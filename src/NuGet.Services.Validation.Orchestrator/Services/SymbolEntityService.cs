// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The EntityService for the <see cref="NuGetGallery.SymbolPackage"/>.
    /// </summary>
    public class SymbolEntityService : IEntityService<SymbolPackage>
    {
        private ICoreSymbolPackageService _galleryEntityService;
        private IEntityRepository<SymbolPackage> _symbolsPackageRepository;

        public SymbolEntityService(ICoreSymbolPackageService galleryEntityService, IEntityRepository<SymbolPackage> symbolsPackageRepository)
        {
            _galleryEntityService = galleryEntityService ?? throw new ArgumentNullException(nameof(galleryEntityService));
            _symbolsPackageRepository = symbolsPackageRepository ?? throw new ArgumentNullException(nameof(symbolsPackageRepository));
        }

        /// <summary>
        /// Only the package symbols that are in validating state will be sent to the symbols validation/ingestion. 
        /// </summary>
        /// <param name="id">The id of the package.</param>
        /// <param name="version">The version of the package.</param>
        /// <returns></returns>
        public IValidatingEntity<SymbolPackage> FindPackageByIdAndVersionStrict(string id, string version)
        {
            var symbolPackage = _galleryEntityService
                .FindSymbolPackagesByIdAndVersion(id, version)
                .Where(s => s.StatusKey == PackageStatus.Validating)
                .FirstOrDefault();

            return symbolPackage == null ? null : new SymbolPackageValidatingEntity(symbolPackage);
        }

        public IValidatingEntity<SymbolPackage> FindPackageByKey(int key)
        {
            var symbolPackage = _symbolsPackageRepository
                   .GetAll()
                   .Include(sp => sp.Package)
                   .Include(sp => sp.Package.PackageRegistration)
                   .SingleOrDefault(sp => sp.Key == key);
            return symbolPackage == null ? null : new SymbolPackageValidatingEntity(symbolPackage);
        }

        public async Task UpdateStatusAsync(SymbolPackage entity, PackageStatus newStatus, bool commitChanges = true)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (newStatus == entity.StatusKey)
            {
                return;
            }

            if (newStatus == PackageStatus.Available)
            {
                var previousAvailableSymbolPackage = _galleryEntityService
                    .FindSymbolPackagesByIdAndVersion(entity.Package.PackageRegistration.Id, entity.Package.NormalizedVersion)
                    .FirstOrDefault(s => s.StatusKey == PackageStatus.Available);

                if (previousAvailableSymbolPackage != null)
                {
                    await _galleryEntityService.UpdateStatusAsync(previousAvailableSymbolPackage, PackageStatus.Deleted, commitChanges: false);
                }
            }

            await _galleryEntityService.UpdateStatusAsync(entity, newStatus, commitChanges);
        }

        public async Task UpdateMetadataAsync(SymbolPackage entity, object metadata, bool commitChanges = true)
        {
            // No action for symbols 
            // For each new symbol a new entry will be added to db and the file symbols will be overwriten
            await Task.CompletedTask;
        }
    }
}
