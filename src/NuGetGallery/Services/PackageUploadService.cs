// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {
        private static readonly IReadOnlyCollection<string> AllowedLicenseFileExtensions = new HashSet<string>
        {
            "",
            ".txt",
            ".md",
        };

        private static readonly IReadOnlyCollection<string> AllowedIconFileExtensions = new HashSet<string>
        {
            ".jpg",
            ".jpeg",
            ".png"
        };

        private static readonly IReadOnlyCollection<string> AllowedLicenseTypes = new HashSet<string>
        {
            LicenseType.File.ToString(),
            LicenseType.Expression.ToString()
        };

        /// <summary>
        /// The upper limit on allowed license file size.
        /// </summary>
        /// <remarks>
        /// This limit is chosen fairly arbitrarily, it has to be large enough to fit any sensible license
        /// in plain text and small enough to not cause issues with scanning through such file a few times
        /// during the package validation.
        /// </remarks>
        private const long MaxAllowedLicenseLengthForUploading = 1024 * 1024; // 1 MB
        private const long MaxAllowedIconLengthForUploading = 1024 * 1024; // 1 MB
        private const int MaxAllowedLicenseNodeValueLength = 500;
        private const string LicenseNodeName = "license";
        private const string IconNodeName = "icon";
        private const string AllowedLicenseVersion = "1.0.0";
        private const string Unlicensed = "UNLICENSED";

        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IValidationService _validationService;
        private readonly IAppConfiguration _config;
        private readonly ITyposquattingService _typosquattingService;
        private readonly ITelemetryService _telemetryService;
        private readonly ICoreLicenseFileService _coreLicenseFileService;
        private readonly IDiagnosticsSource _trace;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IPackageVulnerabilityService _vulnerabilityService;

        public PackageUploadService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IReservedNamespaceService reservedNamespaceService,
            IValidationService validationService,
            IAppConfiguration config,
            ITyposquattingService typosquattingService,
            ITelemetryService telemetryService,
            ICoreLicenseFileService coreLicenseFileService,
            IDiagnosticsService diagnosticsService,
            IFeatureFlagService featureFlagService,
            IPackageVulnerabilityService vulnerabilityService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _typosquattingService = typosquattingService ?? throw new ArgumentNullException(nameof(typosquattingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }
            _trace = diagnosticsService.GetSource(nameof(PackageUploadService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _vulnerabilityService = vulnerabilityService ?? throw new ArgumentNullException(nameof(vulnerabilityService));
        }

        public async Task<PackageValidationResult> ValidateBeforeGeneratePackageAsync(
            PackageArchiveReader nuGetPackage,
            PackageMetadata packageMetadata,
            User currentUser)
        {
            var warnings = new List<IValidationMessage>();

            var result = await CheckPackageEntryCountAsync(nuGetPackage, warnings);

            if (result != null)
            {
                return result;
            }

            var nuspecFileEntry = nuGetPackage.GetEntry(nuGetPackage.GetNuspecFile());
            using (var nuspecFileStream = await nuGetPackage.GetNuspecAsync(CancellationToken.None))
            {
                if (!await IsStreamLengthMatchesReportedAsync(nuspecFileStream, nuspecFileEntry.Length))
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_CorruptNupkg);
                }
            }

            result = await CheckForUnsignedPushAfterAuthorSignedAsync(
                nuGetPackage,
                warnings);

            if (result != null)
            {
                return result;
            }

            result = CheckRepositoryMetadata(packageMetadata, warnings);

            if (result != null)
            {
                return result;
            }

            result = await CheckLicenseMetadataAsync(nuGetPackage, warnings, currentUser);
            if (result != null)
            {
                _telemetryService.TrackLicenseValidationFailure();
                return result;
            }

            result = await CheckIconMetadataAsync(nuGetPackage, warnings, currentUser);
            if (result != null)
            {
                //_telemetryService.TrackIconValidationFailure();
                return result;
            }

            return PackageValidationResult.AcceptedWithWarnings(warnings);
        }

        private async Task<PackageValidationResult> CheckLicenseMetadataAsync(PackageArchiveReader nuGetPackage, List<IValidationMessage> warnings, User user)
        {
            var nuspecReader = GetNuspecReader(nuGetPackage);

            var licenseElement = nuspecReader.LicenseElement;

            if (licenseElement != null)
            {
                if (licenseElement.Value.Length > MaxAllowedLicenseNodeValueLength)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseNodeValueTooLong);
                }

                if (HasChildElements(licenseElement))
                {
                    return PackageValidationResult.Invalid(string.Format(Strings.UploadPackage_NodeContainsChildren, LicenseNodeName));
                }

                var typeText = GetLicenseType(licenseElement);

                if (!AllowedLicenseTypes.Contains(typeText, StringComparer.OrdinalIgnoreCase))
                {
                    return PackageValidationResult.Invalid(string.Format(Strings.UploadPackage_UnsupportedLicenseType, typeText));
                }

                var versionText = GetLicenseVersion(licenseElement);

                if (versionText != null && AllowedLicenseVersion != versionText)
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_UnsupportedLicenseVersion,
                            versionText));
                }
            }

            var licenseUrl = nuspecReader.GetLicenseUrl();
            var licenseMetadata = nuspecReader.GetLicenseMetadata();
            var licenseDeprecationUrl = GetExpectedLicenseUrl(licenseMetadata);

            if (licenseMetadata == null)
            {
                if (string.IsNullOrWhiteSpace(licenseUrl))
                {
                    if (!_config.AllowLicenselessPackages)
                    {
                        return PackageValidationResult.Invalid(new LicenseUrlDeprecationValidationMessage(Strings.UploadPackage_MissingLicenseInformation));
                    }
                    else
                    {
                        warnings.Add(new LicenseUrlDeprecationValidationMessage(Strings.UploadPackage_LicenseShouldBeSpecified));
                    }
                }

                if (licenseDeprecationUrl == licenseUrl)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_DeprecationUrlUsage);
                }

                if (!string.IsNullOrWhiteSpace(licenseUrl))
                {
                    if (_config.BlockLegacyLicenseUrl)
                    {
                        return PackageValidationResult.Invalid(new LicenseUrlDeprecationValidationMessage(Strings.UploadPackage_LegacyLicenseUrlNotAllowed));
                    }
                    else
                    {
                        warnings.Add(new LicenseUrlDeprecationValidationMessage(Strings.UploadPackage_DeprecatingLicenseUrl));
                    }
                }

                // we will return here, so the code below would not need to check for licenseMetadata to be non-null over and over.
                return null;
            }

            if (licenseMetadata.WarningsAndErrors != null && licenseMetadata.WarningsAndErrors.Any())
            {
                _telemetryService.TrackInvalidLicenseMetadata(licenseMetadata.License);
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_InvalidLicenseMetadata,
                        string.Join(" ", licenseMetadata.WarningsAndErrors)));
            }

            if (licenseDeprecationUrl != licenseUrl)
            {
                if (IsMalformedDeprecationUrl(licenseUrl))
                {
                    return PackageValidationResult.Invalid(new InvalidUrlEncodingForLicenseUrlValidationMessage());
                }

                if (licenseMetadata.Type == LicenseType.File)
                {
                    return PackageValidationResult.Invalid(
                        new InvalidLicenseUrlValidationMessage(
                            string.Format(Strings.UploadPackage_DeprecationUrlRequiredForLicenseFiles, licenseDeprecationUrl)));
                }
                else if (licenseMetadata.Type == LicenseType.Expression)
                {
                    return PackageValidationResult.Invalid(
                        new InvalidLicenseUrlValidationMessage(
                            string.Format(Strings.UploadPackage_DeprecationUrlRequiredForLicenseExpressions, licenseDeprecationUrl)));
                }
            }

            if (licenseMetadata.Type == LicenseType.File)
            {
                // fix the path separator. Client enforces forward slashes in all file paths when packing
                var licenseFilename = FileNameHelper.GetZipEntryPath(licenseMetadata.License);
                if (licenseFilename != licenseMetadata.License)
                {
                    var packageIdentity = nuspecReader.GetIdentity();
                    _trace.Information($"Transformed license file name from `{licenseMetadata.License}` to `{licenseFilename}` for package {packageIdentity.Id} {packageIdentity.Version}");
                }

                // check if specified file is present in the package
                if (!FileExists(nuGetPackage, licenseFilename))
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_FileDoesNotExist,
                            Strings.UploadPackage_LicenseFileType,
                            licenseFilename));
                }

                // check if specified file has allowed extension
                var licenseFileExtension = Path.GetExtension(licenseFilename);
                if (!AllowedLicenseFileExtensions.Contains(licenseFileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_InvalidLicenseFileExtension,
                            licenseFileExtension,
                            string.Join(", ", AllowedLicenseFileExtensions.Where(x => x != string.Empty).Select(extension => $"'{extension}'"))));
                }

                var licenseFileEntry = nuGetPackage.GetEntry(licenseFilename);
                if (licenseFileEntry.Length > MaxAllowedLicenseLengthForUploading)
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_FileTooLong,
                            Strings.UploadPackage_LicenseFileType,
                            MaxAllowedLicenseLengthForUploading.ToUserFriendlyBytesLabel()));
                }

                if (!await IsStreamLengthMatchesReportedAsync(nuGetPackage, licenseFilename, licenseFileEntry.Length))
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_CorruptNupkg);
                }

                // zip streams do not support seeking, so we'll have to reopen them
                using (var licenseFileStream = nuGetPackage.GetStream(licenseFilename))
                {
                    // check if specified file is a text file
                    if (!await TextHelper.LooksLikeUtf8TextStreamAsync(licenseFileStream))
                    {
                        return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseMustBePlainText);
                    }
                }
            }

            if (licenseMetadata.Type == LicenseType.Expression)
            {
                if (licenseMetadata.LicenseExpression == null)
                {
                    throw new InvalidOperationException($"Unexpected value of {nameof(licenseMetadata.LicenseExpression)} property");
                }

                var licenseList = GetLicenseList(licenseMetadata.LicenseExpression);
                var unapprovedLicenses = licenseList.Where(license => !license.IsOsiApproved && !license.IsFsfLibre).ToList();
                if (unapprovedLicenses.Any())
                {
                    _telemetryService.TrackNonFsfOsiLicenseUse(licenseMetadata.License);
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_NonFsfOrOsiLicense, string.Join(", ", unapprovedLicenses.Select(l => l.LicenseID))));
                }
            }

            return null;
        }

        private async Task<PackageValidationResult> CheckIconMetadataAsync(PackageArchiveReader nuGetPackage, List<IValidationMessage> warnings, User user)
        {
            var nuspecReader = GetNuspecReader(nuGetPackage);
            var iconElement = nuspecReader.IconElement;
            var embeddedIconsEnabled = _featureFlagService.AreEmbeddedIconsEnabled(user);

            if (iconElement == null)
            {
                if (embeddedIconsEnabled && !string.IsNullOrWhiteSpace(nuspecReader.GetIconUrl()))
                {
                    warnings.Add(new IconUrlDeprecationValidationMessage());
                }
                return null;
            }

            if (!embeddedIconsEnabled)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_EmbeddedIconNotAccepted);
            }

            if (HasChildElements(iconElement))
            {
                return PackageValidationResult.Invalid(string.Format(Strings.UploadPackage_NodeContainsChildren, IconNodeName));
            }

            var iconFilePath = FileNameHelper.GetZipEntryPath(iconElement.Value);
            if (!FileExists(nuGetPackage, iconFilePath))
            {
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_FileDoesNotExist,
                        Strings.UploadPackage_IconFileType,
                        iconFilePath));
            }

            var iconFileExtension = Path.GetExtension(iconFilePath);
            if (!AllowedIconFileExtensions.Contains(iconFileExtension, StringComparer.OrdinalIgnoreCase))
            {
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_InvalidIconFileExtension,
                        iconFileExtension,
                        string.Join(", ", AllowedIconFileExtensions.Where(x => x != string.Empty).Select(extension => $"'{extension}'"))));
            }

            var iconFileEntry = nuGetPackage.GetEntry(iconFilePath);
            if (iconFileEntry.Length > MaxAllowedIconLengthForUploading)
            {
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_FileTooLong,
                        Strings.UploadPackage_IconFileType,
                        MaxAllowedLicenseLengthForUploading.ToUserFriendlyBytesLabel()));
            }

            if (!await IsStreamLengthMatchesReportedAsync(nuGetPackage, iconFilePath, iconFileEntry.Length))
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_CorruptNupkg);
            }

            bool isJpg = await IsJpegAsync(nuGetPackage, iconFilePath);
            bool isPng = await IsPngAsync(nuGetPackage, iconFilePath);

            if (!isPng && !isJpg)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_UnsupportedIconImageFormat);
            }

            return null;
        }

        private static async Task<bool> FileMatchesPredicate(PackageArchiveReader nuGetPackage, string filePath, Func<Stream, Task<bool>> predicate)
        {
            using (var stream = await nuGetPackage.GetStreamAsync(filePath, CancellationToken.None))
            {
                return await predicate(stream);
            }
        }

        private static async Task<bool> IsPngAsync(PackageArchiveReader nuGetPackage, string iconPath)
        {
            return await FileMatchesPredicate(nuGetPackage, iconPath, stream => stream.NextBytesMatchPngHeaderAsync());
        }

        private static async Task<bool> IsJpegAsync(PackageArchiveReader nuGetPackage, string iconPath)
        {
            return await FileMatchesPredicate(nuGetPackage, iconPath, stream => stream.NextBytesMatchJpegHeaderAsync());
        }

        private static bool FileExists(PackageArchiveReader nuGetPackage, string filename)
        {
            var fileList = new HashSet<string>(nuGetPackage.GetFiles());
            return fileList.Contains(filename);
        }

        private static UserContentEnabledNuspecReader GetNuspecReader(PackageArchiveReader nuGetPackage)
        {
            using (var nuspec = nuGetPackage.GetNuspec())
            {
                return new UserContentEnabledNuspecReader(nuspec);
            }
        }

        private bool IsMalformedDeprecationUrl(string licenseUrl)
        {
            // nuget.exe 4.9.0 and its dotnet and msbuild counterparts encode spaces as "+"
            // when generating legacy license URL, which is bad. We explicitly forbid such
            // URLs. On the other hand, if license expression does not contain spaces they
            // generate good URLs which we don't want to reject. This method detects the 
            // case when spaces are in the expression in a meaningful way.

            if (Uri.TryCreate(licenseUrl, UriKind.Absolute, out var url))
            {
                if (url.Host != LicenseExpressionRedirectUrlHelper.LicenseExpressionHostname)
                {
                    return false;
                }

                var invalidUrlBits = new string[] { "+OR+", "+AND+", "+WITH+" };
                return invalidUrlBits.Any(invalidBit => licenseUrl.IndexOf(invalidBit, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return false;
        }

        private List<LicenseData> GetLicenseList(NuGetLicenseExpression licenseExpression)
        {
            var licenseList = new List<LicenseData>();
            var queue = new Queue<NuGetLicenseExpression>();
            queue.Enqueue(licenseExpression);
            while (queue.Any())
            {
                var head = queue.Dequeue();
                switch (head.Type)
                {
                    case LicenseExpressionType.License:
                        {
                            var license = (NuGetLicense)head;
                            var licenseData = NuGetLicenseData.LicenseList[license.Identifier];
                            licenseList.Add(licenseData);
                        }
                        break;
                    case LicenseExpressionType.Operator:
                        var op = (LicenseOperator)head;
                        if (op.OperatorType == LicenseOperatorType.LogicalOperator)
                        {
                            var logicalOperator = (LogicalOperator)op;
                            queue.Enqueue(logicalOperator.Left);
                            queue.Enqueue(logicalOperator.Right);
                        }
                        else if (op.OperatorType == LicenseOperatorType.WithOperator)
                        {
                            var withOperator = (WithOperator)op;
                            var licenseData = NuGetLicenseData.LicenseList[withOperator.License.Identifier];
                            licenseList.Add(licenseData);
                            // exceptions don't interest us for now
                        }
                        break;
                }
            }

            return licenseList;
        }

        private static async Task<bool> IsStreamLengthMatchesReportedAsync(PackageArchiveReader nuGetPackage, string path, long reportedLength)
        {
            using (var stream = await nuGetPackage.GetStreamAsync(path, CancellationToken.None))
            {
                return await IsStreamLengthMatchesReportedAsync(stream, reportedLength);
            }
        }

        private static async Task<bool> IsStreamLengthMatchesReportedAsync(Stream stream, long reportedLength)
        {
            // one may modify the zip file to report smaller file sizes for the compressed files than actual.
            // Unfortunately, .Net's ZipArchive is not handling this case properly and allows to read full
            // data without throwing any exceptions.
            // so we'll try reading the stream ourselves and check if reported length matches the actual.

            var buffer = new byte[4096];
            long totalBytesRead = 0;
            int read = 0;
            do
            {
                read = await stream.ReadAsync(buffer, 0, buffer.Length);
                totalBytesRead += read;
            } while (read > 0 && totalBytesRead < reportedLength + 1); // we want to try to read past the reported length

            return totalBytesRead == reportedLength;
        }

        private static string GetExpectedLicenseUrl(LicenseMetadata licenseMetadata)
        {
            if (licenseMetadata == null || LicenseType.File == licenseMetadata.Type)
            {
                return GalleryConstants.LicenseDeprecationUrl;
            }

            if (LicenseType.Expression == licenseMetadata.Type)
            {
                return LicenseExpressionRedirectUrlHelper.GetLicenseExpressionRedirectUrl(licenseMetadata.License);
            }

            throw new InvalidOperationException($"Unsupported license metadata type: {licenseMetadata.Type}");
        }

        private static bool HasChildElements(XElement xElement)
            => xElement.Elements().Any();

        private static string GetLicenseVersion(XElement licenseElement)
            => licenseElement
                .GetOptionalAttributeValue("version");

        private static string GetLicenseType(XElement licenseElement)
            => licenseElement
                .GetOptionalAttributeValue("type");

        private class UserContentEnabledNuspecReader : NuspecReader
        {
            public UserContentEnabledNuspecReader(Stream stream)
                : base(stream)
            {
            }

            public XElement LicenseElement => MetadataNode.Element(MetadataNode.Name.Namespace + LicenseNodeName);
            public XElement IconElement => MetadataNode.Element(MetadataNode.Name.Namespace + IconNodeName);
        }

        private async Task<PackageValidationResult> CheckPackageEntryCountAsync(
            PackageArchiveReader nuGetPackage,
            List<IValidationMessage> warnings)
        {
            if (!_config.RejectPackagesWithTooManyPackageEntries)
            {
                return null;
            }

            const ushort maxPackageEntryCount = ushort.MaxValue - 1;

            var packageEntryCount = nuGetPackage.GetFiles().Count();

            if (await nuGetPackage.IsSignedAsync(CancellationToken.None))
            {
                if (packageEntryCount > maxPackageEntryCount)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_PackageContainsTooManyEntries);
                }
            }
            else if (packageEntryCount >= maxPackageEntryCount)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_PackageContainsTooManyEntries);
            }

            return null;
        }

        /// <summary>
        /// Validate repository metadata: 
        /// 1. If the type is "git" - allow the URL scheme "git://" or "https://". We will translate "git://" to "https://" at display time for known domains.
        /// 2. For types other then "git" - URL scheme should be "https://"
        /// </summary>
        private PackageValidationResult CheckRepositoryMetadata(PackageMetadata packageMetadata, List<IValidationMessage> warnings)
        {
            if (packageMetadata.RepositoryUrl == null)
            {
                return null;
            }

            // git repository type
            if (PackageHelper.IsGitRepositoryType(packageMetadata.RepositoryType))
            {
                if (!packageMetadata.RepositoryUrl.IsGitProtocol() && !packageMetadata.RepositoryUrl.IsHttpsProtocol())
                {
                    warnings.Add(new PlainTextOnlyValidationMessage(Strings.WarningNotHttpsOrGitRepositoryUrlScheme));
                }
            }
            else
            {
                if (!packageMetadata.RepositoryUrl.IsHttpsProtocol())
                {
                    warnings.Add(new PlainTextOnlyValidationMessage(Strings.WarningNotHttpsRepositoryUrlScheme));
                }
            }

            return null;
        }

        /// <summary>
        /// If a package author pushes version X that is author signed then pushes version Y that is unsigned, where Y
        /// is immediately after X when the version list is sorted used SemVer 2.0.0 rules, warn the package author.
        /// If the user pushes another unsigned version after Y, no warning is produced. This means the warning will
        /// not present on every subsequent push, which would be a bit too noisy.
        /// </summary>
        /// <param name="nuGetPackage">The package archive reader.</param>
        /// <param name="warnings">The working list of warnings.</param>
        /// <returns>The package validation result or null.</returns>
        private async Task<PackageValidationResult> CheckForUnsignedPushAfterAuthorSignedAsync(
            PackageArchiveReader nuGetPackage,
            List<IValidationMessage> warnings)
        {
            // If the package is signed, there's no problem.
            if (await nuGetPackage.IsSignedAsync(CancellationToken.None))
            {
                return null;
            }

            var newIdentity = nuGetPackage.GetIdentity();
            var packageRegistration = _packageService.FindPackageRegistrationById(newIdentity.Id);

            // If the package registration does not exist yet, there's no problem.
            if (packageRegistration == null)
            {
                return null;
            }

            // Find the highest package version less than the new package that is Available. Deleted packages should
            // be ignored and Validating or FailedValidation packages will not necessarily have certificate information.
            var previousPackage = packageRegistration
                .Packages
                .Where(x => x.PackageStatusKey == PackageStatus.Available)
                .Select(x => new { x.NormalizedVersion, x.CertificateKey })
                .ToList() // Materialize the lazy collection.
                .Select(x => new { Version = NuGetVersion.Parse(x.NormalizedVersion), x.CertificateKey })
                .Where(x => x.Version < newIdentity.Version)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();

            if (previousPackage != null && previousPackage.CertificateKey.HasValue)
            {
                warnings.Add(new PlainTextOnlyValidationMessage(
                    string.Format(
                        Strings.UploadPackage_SignedToUnsignedTransition,
                        previousPackage.Version.ToNormalizedString())));
            }

            return null;
        }

        public async Task<PackageValidationResult> ValidateAfterGeneratePackageAsync(
            Package package,
            PackageArchiveReader nuGetPackage,
            User owner,
            User currentUser,
            bool isNewPackageRegistration)
        {
            var result = await ValidateSignatureFilePresenceAsync(
                package.PackageRegistration,
                nuGetPackage,
                owner,
                currentUser);
            if (result != null)
            {
                return result;
            }

            if (isNewPackageRegistration && _typosquattingService.IsUploadedPackageIdTyposquatting(package.Id, owner, out List<string> typosquattingCheckCollisionIds))
            {
                return PackageValidationResult.Invalid(string.Format(Strings.TyposquattingCheckFails, string.Join(",", typosquattingCheckCollisionIds)));
            }

            return PackageValidationResult.Accepted();
        }

        private async Task<PackageValidationResult> ValidateSignatureFilePresenceAsync(
            PackageRegistration packageRegistration,
            PackageArchiveReader nugetPackage,
            User owner,
            User currentUser)
        {
            if (await nugetPackage.IsSignedAsync(CancellationToken.None))
            {
                if (_config.RejectSignedPackagesWithNoRegisteredCertificate
                    && !packageRegistration.IsSigningAllowed())
                {
                    var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();
                    var hasRequiredSigner = requiredSigner != null;

                    if (hasRequiredSigner)
                    {
                        if (requiredSigner == currentUser)
                        {
                            return PackageValidationResult.Invalid(new PackageShouldNotBeSignedUserFixableValidationMessage());
                        }
                        else
                        {
                            return PackageValidationResult.Invalid(
                               string.Format(
                                   Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner,
                                   requiredSigner.Username));
                        }
                    }
                    else
                    {
                        var isCurrentUserAnOwner = packageRegistration.Owners.Contains(currentUser);

                        // Technically, if there is no required signer, any one of the owners can register a
                        // certificate to resolve this issue. However, we favor either the current user or the provided
                        // owner since these are both accounts the current user can push on behalf of. In other words
                        // we provide a message that leads the current user to remedying the problem rather than asking
                        // someone else for help.
                        if (isCurrentUserAnOwner)
                        {
                            return PackageValidationResult.Invalid(new PackageShouldNotBeSignedUserFixableValidationMessage());
                        }
                        else
                        {
                            return PackageValidationResult.Invalid(
                                string.Format(
                                    Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner,
                                    owner.Username));
                        }
                    }
                }
            }
            else
            {
                if (packageRegistration.IsSigningRequired())
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_PackageIsNotSigned);
                }
            }

            return null;
        }

        public async Task<Package> GeneratePackageAsync(
            string id,
            PackageArchiveReader nugetPackage,
            PackageStreamMetadata packageStreamMetadata,
            User owner,
            User currentUser)
        {
            var shouldMarkIdVerified = _reservedNamespaceService.ShouldMarkNewPackageIdVerified(owner, id, out var ownedMatchingReservedNamespaces);

            var package = await _packageService.CreatePackageAsync(
                nugetPackage,
                packageStreamMetadata,
                owner,
                currentUser,
                isVerified: shouldMarkIdVerified);

            if (shouldMarkIdVerified)
            {
                // Add all relevant package registrations to the applicable namespaces
                foreach (var rn in ownedMatchingReservedNamespaces)
                {
                    _reservedNamespaceService.AddPackageRegistrationToNamespace(
                        rn.Value,
                        package.PackageRegistration);
                }
            }

            _vulnerabilityService.ApplyExistingVulnerabilitiesToPackage(package);

            return package;
        }

        public async Task<PackageCommitResult> CommitPackageAsync(Package package, Stream packageFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            if (!packageFile.CanSeek)
            {
                throw new ArgumentException($"{nameof(packageFile)} argument must be seekable stream", nameof(packageFile));
            }

            await _validationService.UpdatePackageAsync(package);

            if (package.PackageStatusKey != PackageStatus.Available
                && package.PackageStatusKey != PackageStatus.Validating)
            {
                throw new ArgumentException(
                    $"The package to commit must have either the {PackageStatus.Available} or {PackageStatus.Validating} package status.",
                    nameof(package));
            }

            try
            {
                if (package.PackageStatusKey == PackageStatus.Validating)
                {
                    await _packageFileService.SaveValidationPackageFileAsync(package, packageFile);

                    /* Suppose two package upload requests come in at the same time with the same package (same ID and
                     * version). It's possible that one request has committed and validated the package AFTER the other
                     * request has checked that this package does not exist in the database. Observe the following
                     * sequence of events to understand why the packages container check is necessary.
                     * 
                     * Request | Step                                           | Component        | Success | Notes
                     * ------- | ---------------------------------------------- | ---------------- | ------- | -----
                     * 1       | version should not exist in DB                 | gallery          | TRUE    | 1st duplicate check (catches most cases over time)
                     * 2       | version should not exist in DB                 | gallery          | TRUE    |
                     * 1       | upload to validation container                 | gallery          | TRUE    | 2nd duplicate check (relevant with high concurrency)
                     * 1       | version should not exist in packages container | gallery          | TRUE    | 3rd duplicate check (relevant with fast validations)
                     * 1       | commit to DB                                   | gallery          | TRUE    |
                     * 1       | upload to packages container                   | async validation | TRUE    |
                     * 1       | move package to Available status in DB         | async validation | TRUE    |
                     * 1       | delete from validation container               | async validation | TRUE    |
                     * 2       | upload to validation container                 | gallery          | TRUE    |
                     * 2       | version should not exist in packages container | gallery          | FALSE   |
                     * 2       | delete from validation (rollback)              | gallery          | TRUE    | Only occurs in the failure case, as a clean-up.
                     *
                     * Alternatively, we could handle the DB conflict exception that would occur in request 2, but this
                     * would result in an exception that can be avoided and require some ugly code that teases the
                     * unique constraint failure out of a SqlException.
                     * 
                     * Another alternative is always leaving the package in the validation container. This is not great
                     * since it doubles the amount of space we need to store packages. Also, it complicates the soft or
                     * hard package delete flow.
                     * 
                     * We can safely delete the validation package because we know it's ours. We know this because
                     * saving the validation package succeeded, meaning async validation already successfully moved the
                     * previous package (request 1's package) from the validation container to the package container
                     * and transitioned the package to Available status.
                     * 
                     * See the following issue in GitHub for how this case was found:
                     * https://github.com/NuGet/NuGetGallery/issues/5039
                     */
                    if (await _packageFileService.DoesPackageFileExistAsync(package))
                    {
                        await _packageFileService.DeleteValidationPackageFileAsync(
                            package.PackageRegistration.Id,
                            package.Version);

                        return PackageCommitResult.Conflict;
                    }
                }
                else
                {
                    if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        // if the package is immediately made available, it means there is a high chance we don't have
                        // validation pipeline that would normally store the license file, so we'll do it ourselves here.
                        await _coreLicenseFileService.ExtractAndSaveLicenseFileAsync(package, packageFile);
                    }
                    try
                    {
                        packageFile.Seek(0, SeekOrigin.Begin);
                        await _packageFileService.SavePackageFileAsync(package, packageFile);
                    }
                    catch when (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        await _coreLicenseFileService.DeleteLicenseFileAsync(
                            package.PackageRegistration.Id,
                            package.NormalizedVersion);
                        throw;
                    }
                }
            }
            catch (FileAlreadyExistsException ex)
            {
                ex.Log();
                return PackageCommitResult.Conflict;
            }

            try
            {
                // Sending the validation request after copying to prevent multiple validation requests
                // sent when several pushes for the same package happen concurrently. Copying the file
                // resolves the race and only one request will "win" and reach this code.
                await _validationService.StartValidationAsync(package);

                // commit all changes to database as an atomic transaction
                await _entitiesContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // If sending the validation request or saving to the DB fails for any reason
                // we need to delete the package we just saved.
                if (package.PackageStatusKey == PackageStatus.Validating)
                {
                    await _packageFileService.DeleteValidationPackageFileAsync(
                        package.PackageRegistration.Id,
                        package.Version);
                }
                else
                {
                    await _packageFileService.DeletePackageFileAsync(
                        package.PackageRegistration.Id,
                        package.Version);
                    await _coreLicenseFileService.DeleteLicenseFileAsync(
                        package.PackageRegistration.Id,
                        package.NormalizedVersion);
                }

                return ReturnConflictOrThrow(ex);
            }

            return PackageCommitResult.Success;
        }

        private PackageCommitResult ReturnConflictOrThrow(Exception ex)
        {
            if (ex is DbUpdateConcurrencyException concurrencyEx)
            {
                return PackageCommitResult.Conflict;
            }
            else if (ex is DbUpdateException dbUpdateEx)
            {
                if (dbUpdateEx.InnerException?.InnerException != null)
                {
                    if (dbUpdateEx.InnerException.InnerException is SqlException sqlException)
                    {
                        switch (sqlException.Number)
                        {
                            case 547:   // Constraint check violation
                            case 2601:  // Duplicated key row error
                            case 2627:  // Unique constraint error
                                return PackageCommitResult.Conflict;
                        }
                    }
                }
            }

            throw ex;
        }
    }
}
