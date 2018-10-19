// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Extensions;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {
        const string LicenseDeprecationUrl = "https://aka.ms/deprecateLicenseUrl";

        private static readonly IReadOnlyCollection<string> AllowedLicenseFileExtensions = new HashSet<string>
        {
            ".txt",
            ".md",
        };

        private const string LicenseNodeName = "license";
        private const int MaxAllowedLicenseLength = 1024 * 1024;
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
            var warnings = new List<string>();

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

            result = CheckLicenseMetadata(nuGetPackage, warnings);
            if (result != null)
            {
                return result;
            }

            return PackageValidationResult.AcceptedWithWarnings(warnings);
        }

        private PackageValidationResult CheckLicenseMetadata(PackageArchiveReader nuGetPackage, List<string> warnings)
        {
            LicenseCheckingNuspecReader nuspecReader = null;
            using (var nuspec = nuGetPackage.GetNuspec())
            {
                nuspecReader = new LicenseCheckingNuspecReader(nuspec);
            }

            if (_config.RejectPackagesWithLicense && nuspecReader.HasLicenseMetadata())
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_NotAcceptingPackagesWithLicense);
            }
            var licenseUrl = nuspecReader.GetLicenseUrl();
            var licenseMetadata = nuspecReader.GetLicenseMetadata();

            if (!_config.AllowLicenselessPackages && string.IsNullOrWhiteSpace(licenseUrl) && licenseMetadata == null)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_MissingLicenseInformation);
            }

            if (LicenseDeprecationUrl == licenseUrl && licenseMetadata == null)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_DeprecationUrlUsage);
            }

            if (!string.IsNullOrWhiteSpace(licenseUrl) && licenseMetadata == null)
            {
                if (_config.BlockLegacyLicenseUrl)
                {
                    return PackageValidationResult.Invalid(Strings.UploadPackage_LegacyLicenseUrlNotAllowed);
                }
                else
                {
                    warnings.Add(Strings.UploadPackage_DeprecatingLicenseUrl);
                }
            }

            if (licenseMetadata != null && licenseMetadata.Type == LicenseType.Expression)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseExpressionsNotSupported);
            }

            if (licenseMetadata != null && LicenseDeprecationUrl != licenseUrl)
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_DeprecationUrlRequired);
            }

            if (licenseMetadata?.WarningsAndErrors != null && licenseMetadata.WarningsAndErrors.Any())
            {
                return PackageValidationResult.Invalid(
                    string.Format(
                        Strings.UploadPackage_InvalidLicenseMetadata,
                        string.Join(" ", licenseMetadata.WarningsAndErrors)));
            }

            if (licenseMetadata != null && licenseMetadata.Type == LicenseType.File)
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
                if (!AllowedLicenseFileExtensions.Contains(licenseFileExtension))
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_InvalidLicenseFileExtension,
                            licenseFileExtension,
                            string.Join(", ", AllowedLicenseFileExtensions)));
                }

                var licenseFileEntry = nuGetPackage.GetEntry(licenseMetadata.License);
                if (licenseFileEntry.Length > MaxAllowedLicenseLength)
                {
                    return PackageValidationResult.Invalid(
                        string.Format(
                            Strings.UploadPackage_LicenseFileTooLong,
                            MaxAllowedLicenseLength));
                }

                // check if specified file is a text file
                using (var licenseFileStream = nuGetPackage.GetStream(licenseMetadata.License))
                {
                    if (!IsTextStream(licenseFileStream))
                    {
                        return PackageValidationResult.Invalid(Strings.UploadPackage_LicenseMustBePlainText);
                    }
                }
            }

            return null;
        }

        private static bool IsTextByte(int byteValue)
        {
            const int TextRangeStart = ' ';
            const int LineFeed = '\n';
            const int CarriageReturn = '\r';
            const int Tab = '\t';

            return byteValue >= TextRangeStart || byteValue == LineFeed || byteValue == CarriageReturn || byteValue == Tab;
        }

        private static bool IsTextStream(Stream stream)
        {
            byte[] buffer = new byte[1];
            int bytesRead;
            do
            {
                bytesRead = stream.Read(buffer, 0, 1);
                if (bytesRead > 0 && !IsTextByte(buffer[0]))
                {
                    return false;
                }
            } while (bytesRead > 0);

            return true;
        }

        
        private class LicenseCheckingNuspecReader : NuspecReader
        {
            public LicenseCheckingNuspecReader(Stream stream)
                : base(stream)
            {

            }

            public bool HasLicenseMetadata()
                => MetadataNode
                    .Elements()
                    .Any(e => LicenseNodeName == e.Name.LocalName);
        }

        private PackageValidationResult CheckLicenseMetadata(PackageArchiveReader nuGetPackage)
        {
            if (!_config.RejectPackagesWithLicense)
            {
                return null;
            }

            LicenseCheckingNuspecReader nuspecReader = null;

            using (var nuspec = nuGetPackage.GetNuspec())
            {
                nuspecReader = new LicenseCheckingNuspecReader(nuspec);
            }

            if (nuspecReader.HasLicenseMetadata())
            {
                return PackageValidationResult.Invalid(Strings.UploadPackage_NotAcceptingPackagesWithLicense);
            }

            return null;
        }

        private async Task<PackageValidationResult> CheckPackageEntryCountAsync(
            PackageArchiveReader nuGetPackage,
            List<string> warnings)
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
        private PackageValidationResult CheckRepositoryMetadata(PackageMetadata packageMetadata, List<string> warnings)
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
                    warnings.Add(Strings.WarningNotHttpsOrGitRepositoryUrlScheme);
                }
            }
            else
            {
                if (!packageMetadata.RepositoryUrl.IsHttpsProtocol())
                {
                    warnings.Add(Strings.WarningNotHttpsRepositoryUrlScheme);
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
            List<string> warnings)
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
                warnings.Add(string.Format(
                    Strings.UploadPackage_SignedToUnsignedTransition,
                    previousPackage.Version.ToNormalizedString()));
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
                            return new PackageValidationResult(
                                PackageValidationResultType.PackageShouldNotBeSignedButCanManageCertificates,
                                Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates);
                        }
                        else
                        {
                            return new PackageValidationResult(
                               PackageValidationResultType.PackageShouldNotBeSigned,
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
                            return new PackageValidationResult(
                                PackageValidationResultType.PackageShouldNotBeSignedButCanManageCertificates,
                                Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates);
                        }
                        else
                        {
                            return new PackageValidationResult(
                                PackageValidationResultType.PackageShouldNotBeSigned,
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
                        // if the package is immediately made avaialble, it means there is a high chance we don't have
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
            catch
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

                throw;
            }

            return PackageCommitResult.Success;
        }

        private static async Task SavePackageLicenseFile(Stream packageFile, Func<Stream, Task> saveLicenseAsync)
        {
            packageFile.Seek(0, SeekOrigin.Begin);
            using (var packageArchiveReader = new PackageArchiveReader(packageFile, leaveStreamOpen: true))
            {
                var packageMetadata = PackageMetadata.FromNuspecReader(packageArchiveReader.GetNuspecReader(), strict: true);
                if (packageMetadata.LicenseMetadata != null && packageMetadata.LicenseMetadata.Type == LicenseType.File && !string.IsNullOrWhiteSpace(packageMetadata.LicenseMetadata.License))
                {
                    var filename = packageMetadata.LicenseMetadata.License;
                    var licenseFileEntry = packageArchiveReader.GetEntry(filename); // throws on non-existent file
                    using (var licenseFileStream = licenseFileEntry.Open())
                    {
                        await saveLicenseAsync(licenseFileStream);
                    }
                }
                else
                {
                    throw new Exception("No license file specified in the nuspec");
                }
            }
        }
    }
}
