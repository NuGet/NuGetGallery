// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Configuration;
using NuGetGallery.Extensions;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IValidationService _validationService;
        private readonly IAppConfiguration _config;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly ISymbolPackageFileService _symbolPackageFileService;

        public PackageUploadService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IReservedNamespaceService reservedNamespaceService,
            IValidationService validationService,
            IAppConfiguration config,
            ISymbolPackageService symbolPackageService,
            ISymbolPackageFileService symbolPackageFileService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _symbolPackageFileService = symbolPackageFileService ?? throw new ArgumentNullException(nameof(symbolPackageFileService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<PackageValidationResult> ValidatePackageAsync(
            Package package,
            PackageArchiveReader nuGetPackage,
            User owner,
            User currentUser)
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

        /// <summary>
        /// This method creates the symbol db entities and invokes the validations for the uploaded snupkg. 
        /// It will send the message for validation and upload the snupkg to the "validations"/"symbols-packages" container
        /// based on the result. It will then update the references in the database for persistence with appropriate status.
        /// </summary>
        /// <param name="package">The package for which symbols package is to be uplloaded</param>
        /// <param name="packageStreamMetadata">The package stream metadata for the uploaded symbols package file.</param>
        /// <param name="symbolPackageFile">The symbol package file stream.</param>
        /// <returns>The <see cref="PackageCommitResult"/> for the symbol package upload flow.</returns>
        public async Task<PackageCommitResult> CreateAndUploadSymbolsPackage(Package package, PackageStreamMetadata packageStreamMetadata, Stream symbolPackageFile)
        {
            var symbolPackage = _symbolPackageService.CreateSymbolPackage(package, packageStreamMetadata);

            // TODO: Add Validations for symbols, for now set the status to Available. https://github.com/NuGet/NuGetGallery/issues/6235 
            // Add validating type to be symbols when sending message to the orchestrator.
            symbolPackage.StatusKey = PackageStatus.Available;

            if (symbolPackage.StatusKey != PackageStatus.Available
                && symbolPackage.StatusKey != PackageStatus.Validating)
            {
                throw new InvalidOperationException(
                    $"The symbol package to commit must have either the {PackageStatus.Available} or {PackageStatus.Validating} package status.");
            }

            try
            {
                if (symbolPackage.StatusKey == PackageStatus.Validating)
                {
                    await _symbolPackageFileService.SaveValidationPackageFileAsync(symbolPackage.Package, symbolPackageFile);
                }
                else if (symbolPackage.StatusKey == PackageStatus.Available)
                {
                    // Mark any other associated available symbol packages for deletion.
                    var availableSymbolPackages = package
                        .SymbolPackages
                        .Where(sp => sp.StatusKey == PackageStatus.Available 
                            && sp != symbolPackage);

                    var overwrite = false;
                    if (availableSymbolPackages.Any())
                    {
                        // Mark the currently available packages for deletion, and replace the file in the container.
                        foreach (var availableSymbolPackage in availableSymbolPackages)
                        {
                            availableSymbolPackage.StatusKey = PackageStatus.Deleted;
                        }

                        overwrite = true;
                    }

                    // Caveat: This doesn't really affect our prod flow since the package is validating, however, when the async validation
                    // is disabled there is a chance that there could be concurrency issues when pushing multiple symbols simultaneously. 
                    // This could result in an inconsistent data or multiple symbol entities marked as available. This could be sovled using etag
                    // for saving files, however since it doesn't really affect nuget.org which happen have async validations flow I will leave it as is.
                    await _symbolPackageFileService.SavePackageFileAsync(symbolPackage.Package, symbolPackageFile, overwrite);
                }

                try
                {
                    // commit all changes to database as an atomic transaction
                    await _entitiesContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    ex.Log();

                    // If saving to the DB fails for any reason we need to delete the package we just saved.
                    if (symbolPackage.StatusKey == PackageStatus.Validating)
                    {
                        await _symbolPackageFileService.DeleteValidationPackageFileAsync(
                            package.PackageRegistration.Id,
                            package.Version);
                    }
                    else if (symbolPackage.StatusKey == PackageStatus.Available)
                    {
                        await _symbolPackageFileService.DeletePackageFileAsync(
                            package.PackageRegistration.Id,
                            package.Version);
                    }

                    throw ex;
                }
            }
            catch (FileAlreadyExistsException ex)
            {
                ex.Log();
                return PackageCommitResult.Conflict;
            }

            return PackageCommitResult.Success;
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
                    await _packageFileService.SavePackageFileAsync(package, packageFile);
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
                }

                throw;
            }

            return PackageCommitResult.Success;
        }
    }
}
