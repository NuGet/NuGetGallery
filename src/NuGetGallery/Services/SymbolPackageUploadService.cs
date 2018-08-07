// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class SymbolPackageUploadService : ISymbolPackageUploadService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IValidationService _validationService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly ISymbolPackageFileService _symbolPackageFileService;

        public SymbolPackageUploadService(
            ISymbolPackageService symbolPackageService,
            ISymbolPackageFileService symbolPackageFileService,
            IEntitiesContext entitiesContext,
            IValidationService validationService)
        {
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _symbolPackageFileService = symbolPackageFileService ?? throw new ArgumentNullException(nameof(symbolPackageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
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

            await _validationService.StartSymbolsPackageValidationAsync(symbolPackage);

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
                    if (symbolPackage.Published == null)
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
    }
}
