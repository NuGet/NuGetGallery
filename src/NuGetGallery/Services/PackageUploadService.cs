// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {
        private const string LicenseExpressionDeprecationUrlFormat = "https://licenses.nuget.org/{0}";

        private static readonly IReadOnlyCollection<string> AllowedLicenseFileExtensions = new HashSet<string>
        {
            "",
            ".txt",
            ".md",
        };

        private static readonly IReadOnlyCollection<string> AllowedLicenseTypes = new HashSet<string>
        {
            LicenseType.File.ToString(),
            LicenseType.Expression.ToString()
        };

        private const string LicenseNodeName = "license";
        private const string AllowedLicenseVersion = "1.0.0";
        private const string Unlicensed = "UNLICENSED";
        private const int MaxAllowedLicenseLength = 1024 * 1024;
        private const int MaxAllowedLicenseNodeValueLength = 500;

        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IValidationService _validationService;
        private readonly IAppConfiguration _config;
        private readonly ITyposquattingService _typosquattingService;

        public PackageUploadService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IReservedNamespaceService reservedNamespaceService,
            IValidationService validationService,
            IAppConfiguration config,
            ITyposquattingService typosquattingService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _typosquattingService = typosquattingService ?? throw new ArgumentNullException(nameof(typosquattingService));
        }

        public async Task<PackageValidationResult> ValidateBeforeGeneratePackageAsync(PackageArchiveReader nuGetPackage, PackageMetadata packageMetadata)
        {
            var warnings = new List<IValidationMessage>();

            var result = await CheckPackageEntryCountAsync(nuGetPackage, warnings);

            if (result != null)
            {
                return result;
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

            result = await CheckLicenseMetadataAsync(nuGetPackage, warnings);
            if (result != null)
            {
                return result;
            }

            return PackageValidationResult.AcceptedWithWarnings(warnings);
        }

        private async Task<PackageValidationResult> CheckLicenseMetadataAsync(PackageArchiveReader nuGetPackage, List<IValidationMessage> warnings)
        {
            LicenseCheckingNuspecReader nuspecReader = null;
            using (var nuspec = nuGetPackage.GetNuspec())
            {
                nuspecReader = new LicenseCheckingNuspecReader(nuspec);
            }

            var licenseElement = nuspecReader.LicenseElement;

            if (licenseElement != null)
            {
                if (_config.RejectPackagesWithLicense)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_NotAcceptingPackagesWithLicense);
                }

                if (licenseElement.Value.Length > MaxAllowedLicenseNodeValueLength)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseNodeValueTooLong);
                }

                if (HasChildElements(licenseElement))
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseNodeContainsChildren);
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

                // TODO: remove when all pipeline changes are done
                if (LicenseType.File.ToString().Equals(typeText, StringComparison.OrdinalIgnoreCase))
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseFilesAreNotAllowed);
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

                // no more checks for missing license metadata case
                return null;
            }

            if (licenseMetadata.WarningsAndErrors != null && licenseMetadata.WarningsAndErrors.Any())
            {
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_InvalidLicenseMetadata,
                        string.Join(" ", licenseMetadata.WarningsAndErrors)));
            }

            if (licenseDeprecationUrl != licenseUrl)
            {
                if (LicenseType.File == licenseMetadata.Type)
                {
                    warnings.Add(
                        new PlainTextOnlyValidationMessage(
                            string.Format(Strings.UploadPackage_DeprecationUrlSuggestedForLicenseFiles, licenseDeprecationUrl)));
                }
                else if (LicenseType.Expression == licenseMetadata.Type)
                {
                    warnings.Add(
                        new PlainTextOnlyValidationMessage(
                            string.Format(Strings.UploadPackage_DeprecationUrlSuggestedForLicenseExpressions, licenseDeprecationUrl)));
                }
            }

            if (LicenseType.File == licenseMetadata.Type)
            {
                // check if specified file is present in the package
                var fileList = new HashSet<string>(nuGetPackage.GetFiles());
                if (!fileList.Contains(licenseMetadata.License))
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_LicenseFileDoesNotExist,
                            licenseMetadata.License));
                }

                // check if specified file has allowed extension
                var licenseFileExtension = Path.GetExtension(licenseMetadata.License);
                if (!AllowedLicenseFileExtensions.Contains(licenseFileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_InvalidLicenseFileExtension,
                            licenseFileExtension,
                            string.Join(", ", AllowedLicenseFileExtensions.Select(extension => $"'{extension}'"))));
                }

                var licenseFileEntry = nuGetPackage.GetEntry(licenseMetadata.License);
                if (licenseFileEntry.Length > MaxAllowedLicenseLength)
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_LicenseFileTooLong,
                            MaxAllowedLicenseLength));
                }

                using (var licenseFileStream = nuGetPackage.GetStream(licenseMetadata.License))
                {
                    if (!await IsStreamLengthMatchesReportedAsync(licenseFileStream, licenseFileEntry.Length))
                    {
                        return PackageValidationResult.Invalid(Strings.UploadPackage_CorruptNupkg);
                    }
                }

                // zip streams do not support seeking, so we'll have to reopen them
                using (var licenseFileStream = nuGetPackage.GetStream(licenseMetadata.License))
                {
                    // check if specified file is a text file
                    if (!await TextHelper.LooksLikeUtf8TextStreamAsync(licenseFileStream))
                    {
                        return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseMustBePlainText);
                    }
                }
            }

            return null;
        }

        private static async Task<bool> IsStreamLengthMatchesReportedAsync(Stream licenseFileStream, long reportedLength)
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
                read = await licenseFileStream.ReadAsync(buffer, 0, buffer.Length);
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
                var expectedUrl = new Uri(string.Format(LicenseExpressionDeprecationUrlFormat, licenseMetadata.License)).AbsoluteUri;
                return expectedUrl;
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

        private class LicenseCheckingNuspecReader : NuspecReader
        {
            public LicenseCheckingNuspecReader(Stream stream)
                : base(stream)
            {
            }

            public XElement LicenseElement => MetadataNode.Element(MetadataNode.Name.Namespace + LicenseNodeName);
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

            return package;
        }

        public async Task<PackageCommitResult> CommitPackageAsync(Package package, Stream packageFile)
        {
            await _validationService.StartValidationAsync(package);

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
                        await SavePackageLicenseFile(packageFile, licenseStream => _packageFileService.SaveLicenseFileAsync(package, licenseStream));
                    }
                    try
                    {
                        await _packageFileService.SavePackageFileAsync(package, packageFile);
                    }
                    catch when (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        await _packageFileService.DeleteLicenseFileAsync(
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
                // commit all changes to database as an atomic transaction
                await _entitiesContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // If saving to the DB fails for any reason we need to delete the package we just saved.
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
                    await _packageFileService.DeleteLicenseFileAsync(
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

        private static async Task SavePackageLicenseFile(Stream packageFile, Func<Stream, Task> saveLicenseAsync)
        {
            packageFile.Seek(0, SeekOrigin.Begin);
            using (var packageArchiveReader = new PackageArchiveReader(packageFile, leaveStreamOpen: true))
            {
                var packageMetadata = PackageMetadata.FromNuspecReader(packageArchiveReader.GetNuspecReader(), strict: true);
                if (packageMetadata.LicenseMetadata == null || packageMetadata.LicenseMetadata.Type != LicenseType.File || string.IsNullOrWhiteSpace(packageMetadata.LicenseMetadata.License))
                {
                    throw new InvalidOperationException("No license file specified in the nuspec");
                }

                var filename = packageMetadata.LicenseMetadata.License;
                var licenseFileEntry = packageArchiveReader.GetEntry(filename); // throws on non-existent file
                using (var licenseFileStream = licenseFileEntry.Open())
                {
                    await saveLicenseAsync(licenseFileStream);
                }
            }
        }
    }
}
