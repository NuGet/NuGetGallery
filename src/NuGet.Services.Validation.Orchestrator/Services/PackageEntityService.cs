// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Data.Entity;
using System.Threading.Tasks;
using NuGetGallery;
using NuGetGallery.Packaging;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// An EntityService for the <see cref="NuGetGallery.Package"/>.
    /// </summary>
    public class PackageEntityService : IEntityService<Package>
    {
        private ICorePackageService _galleryEntityService;
        private IEntityRepository<Package> _packageRepository;

        public PackageEntityService(ICorePackageService galleryEntityService, IEntityRepository<Package> packageRepository)
        {
            _galleryEntityService = galleryEntityService ?? throw new ArgumentNullException(nameof(galleryEntityService));
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
        }

        public IValidatingEntity<Package> FindPackageByIdAndVersionStrict(string id, string version)
        {
            var p = _galleryEntityService.FindPackageByIdAndVersionStrict(id, version);
            return p == null ? null : new PackageValidatingEntity(p);
        }

        public IValidatingEntity<Package> FindPackageByKey(int key)
        {
            var package = _packageRepository
                   .GetAll()
                   .Include(p => p.LicenseReports)
                   .Include(p => p.PackageRegistration)
                   .Include(p => p.User)
                   .Include(p => p.SymbolPackages)
                   .SingleOrDefault(p => p.Key == key);
            return package == null ? null : new PackageValidatingEntity(package);
        }

        public async Task UpdateStatusAsync(Package entity, PackageStatus newStatus, bool commitChanges = true)
        {
            await _galleryEntityService.UpdatePackageStatusAsync(entity, newStatus, commitChanges);
        }

        public async Task UpdateMetadataAsync(Package entity, object metadata, bool commitChanges = true)
        {
            PackageStreamMetadata typedMetadata = metadata == null ? null : metadata as PackageStreamMetadata;
            if (typedMetadata != null)
            {
                if (typedMetadata.Size != entity.PackageFileSize
                    || typedMetadata.Hash != entity.Hash
                    || typedMetadata.HashAlgorithm != entity.HashAlgorithm)
                {
                    await _galleryEntityService.UpdatePackageStreamMetadataAsync(entity, typedMetadata, commitChanges);
                }
            }
        }
    }
}
