// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class SymbolPackageUploadService : ISymbolPackageUploadService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IValidationService _validationService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly ISymbolPackageFileService _symbolPackageFileService;
        private readonly IPackageService _packageService;
        private readonly ITelemetryService _telemetryService;
        private readonly IContentObjectService _contentObjectService;

        public SymbolPackageUploadService(
            ISymbolPackageService symbolPackageService,
            ISymbolPackageFileService symbolPackageFileService,
            IEntitiesContext entitiesContext,
            IValidationService validationService,
            IPackageService packageService,
            ITelemetryService telemetryService,
            IContentObjectService contentObjectService)
        {
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _symbolPackageFileService = symbolPackageFileService ?? throw new ArgumentNullException(nameof(symbolPackageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
        }

        /// <summary>
        /// This method does not perform the ownership validations for the symbols package. It is the responsibility
        /// of the caller to do it. Also, this method does not dispose the <see cref="Stream"/> object, the caller 
        /// should take care of it.
        /// </summary>
        /// <param name="symbolPackageStream"><see cref="Stream"/> object for the symbols package.</param>
        /// <param name="currentUser">The user performing the uploads.</param>
        /// <returns>Awaitable task for <see cref="SymbolPackageValidationResult"/></returns>
        public async Task<SymbolPackageValidationResult> ValidateUploadedSymbolsPackage(Stream symbolPackageStream, User currentUser)
        {
            Package package = null;

            // Check if symbol package upload is allowed for this user.
            if (!_contentObjectService.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(currentUser))
            {
                return SymbolPackageValidationResult.UserNotAllowedToUpload(Strings.SymbolsPackage_UploadNotAllowed);
            }

            try
            {
                if (ZipArchiveHelpers.FoundEntryInFuture(symbolPackageStream, out ZipArchiveEntry entryInTheFuture))
                {
                    return SymbolPackageValidationResult.Invalid(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PackageEntryFromTheFuture,
                        entryInTheFuture.Name));
                }

                using (var packageToPush = new PackageArchiveReader(symbolPackageStream, leaveStreamOpen: true))
                {
                    var nuspec = packageToPush.GetNuspecReader();
                    var id = nuspec.GetId();
                    var version = nuspec.GetVersion();
                    var normalizedVersion = version.ToNormalizedStringSafe();

                    // Ensure the corresponding package exists before pushing a snupkg.
                    package = _packageService.FindPackageByIdAndVersionStrict(id, version.ToStringSafe());
                    if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                    {
                        return SymbolPackageValidationResult.MissingPackage(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.SymbolsPackage_PackageIdAndVersionNotFound,
                            id,
                            normalizedVersion));
                    }

                    // Check for duplicated entries in symbols package
                    if (PackageValidationHelper.HasDuplicatedEntries(packageToPush))
                    {
                        return SymbolPackageValidationResult.Invalid(Strings.UploadPackage_PackageContainsDuplicatedEntries);
                    }

                    // Do not allow to upload a snupkg to a package which has symbols package pending validations.
                    if (package.SymbolPackages.Any(sp => sp.StatusKey == PackageStatus.Validating))
                    {
                        return SymbolPackageValidationResult.SymbolsPackageExists(Strings.SymbolsPackage_ConflictValidating);
                    }

                    try
                    {
                        await _symbolPackageService.EnsureValidAsync(packageToPush);
                    }
                    catch (Exception ex)
                    {
                        ex.Log();

                        var message = Strings.SymbolsPackage_FailedToReadPackage;
                        if (ex is InvalidPackageException || ex is InvalidDataException || ex is EntityException)
                        {
                            message = ex.Message;
                        }

                        _telemetryService.TrackSymbolPackageFailedGalleryValidationEvent(id, normalizedVersion);
                        return SymbolPackageValidationResult.Invalid(message);
                    }
                }

                return SymbolPackageValidationResult.AcceptedForPackage(package);
            }
            catch (Exception ex) when (ex is InvalidPackageException
                || ex is InvalidDataException
                || ex is EntityException
                || ex is FrameworkException)
            {
                return SymbolPackageValidationResult.Invalid(
                    string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_InvalidPackage, ex.Message));
            }
        }

        /// <summary>
        /// This method creates the symbol db entities and invokes the validations for the uploaded snupkg. 
        /// It will send the message for validation and upload the snupkg to the "validations"/"symbols-packages" container
        /// based on the result. It will then update the references in the database for persistence with appropriate status.
        /// </summary>
        /// <param name="package">The package for which symbols package is to be uplloaded</param>
        /// <param name="symbolPackageStream">The symbols package stream metadata for the uploaded symbols package file.</param>
        /// <returns>The <see cref="PackageCommitResult"/> for the create and upload symbol package flow.</returns>
        public async Task<PackageCommitResult> CreateAndUploadSymbolsPackage(Package package, Stream symbolPackageStream)
        {
            var packageStreamMetadata = new PackageStreamMetadata
            {
                HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                Hash = CryptographyService.GenerateHash(
                    symbolPackageStream.AsSeekableStream(),
                    CoreConstants.Sha512HashAlgorithmId),
                Size = symbolPackageStream.Length
            };

            Stream symbolPackageFile = symbolPackageStream.AsSeekableStream();

            var previousSymbolsPackage = package.LatestSymbolPackage();
            var symbolPackage = _symbolPackageService.CreateSymbolPackage(package, packageStreamMetadata);

            await _validationService.UpdatePackageAsync(symbolPackage);

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
                    // If the last uploaded symbol package has failed validation, it will leave the snupkg in the 
                    // validations container. We could possibly overwrite it, but that might introduce a concurrency 
                    // issue on multiple snupkg uploads with a prior failed validation. The best thing to do would be
                    // to delete the failed validation snupkg from validations container and then proceed with normal
                    // upload.
                    if (previousSymbolsPackage != null && previousSymbolsPackage.StatusKey == PackageStatus.FailedValidation)
                    {
                        await DeleteSymbolsPackageAsync(previousSymbolsPackage);
                    }

                    await _symbolPackageFileService.SaveValidationPackageFileAsync(symbolPackage.Package, symbolPackageFile);
                }
                else if (symbolPackage.StatusKey == PackageStatus.Available)
                {
                    if (!symbolPackage.Published.HasValue)
                    {
                        symbolPackage.Published = DateTime.UtcNow;
                    }

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
                    // Sending the validation request right before updating the database, so all file operations
                    // are complete by that time and all possible conflicts are resolved.
                    await _validationService.StartValidationAsync(symbolPackage);

                    // commit all changes to database as an atomic transaction
                    await _entitiesContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    ex.Log();

                    // If sending the validation request or saving to the DB fails for any reason
                    // we need to delete the package we just saved.
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

                    throw;
                }
            }
            catch (FileAlreadyExistsException ex)
            {
                ex.Log();
                return PackageCommitResult.Conflict;
            }

            _telemetryService.TrackSymbolPackagePushEvent(package.Id, package.NormalizedVersion);

            return PackageCommitResult.Success;
        }

        public async Task DeleteSymbolsPackageAsync(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            if (symbolPackage.StatusKey == PackageStatus.FailedValidation
                && await _symbolPackageFileService.DoesValidationPackageFileExistAsync(symbolPackage.Package))
            {
                await _symbolPackageFileService.DeleteValidationPackageFileAsync(symbolPackage.Id, symbolPackage.Version);
            }
            else if (symbolPackage.StatusKey == PackageStatus.Available
                && await _symbolPackageFileService.DoesPackageFileExistAsync(symbolPackage.Package))
            {
                await _symbolPackageFileService.DeletePackageFileAsync(symbolPackage.Id, symbolPackage.Version);
            }

            await _symbolPackageService.UpdateStatusAsync(symbolPackage, PackageStatus.Deleted, commitChanges: true);
        }
    }
}
