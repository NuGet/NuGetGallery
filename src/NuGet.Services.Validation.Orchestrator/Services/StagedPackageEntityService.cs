// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageEntityService : IEntityService<StagedPackage>
    {
        private readonly ICorePackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;

        public StagedPackageEntityService(ICorePackageService packageService, IEntitiesContext entitiesContext)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IValidatingEntity<StagedPackage> FindPackageByIdAndVersionStrict(string id, string version)
        {
            var stagedPackage = GetAll().SingleOrDefault(candidate =>
                candidate.Package.PackageRegistration.Id == id &&
                candidate.Package.NormalizedVersion == version);
            if (stagedPackage == null)
            {
                return null;
            }

            return new StagedPackageValidatingEntity(stagedPackage);
        }

        public IValidatingEntity<StagedPackage> FindPackageByKey(int key)
        {
            var stagedPackage = GetAll().SingleOrDefault(candidate => candidate.PackageKey == key);
            if (stagedPackage == null)
            {
                return null;
            }

            return new StagedPackageValidatingEntity(stagedPackage);
        }

        public async Task UpdateStatusAsync(StagedPackage entity, PackageStatus newStatus, bool commitChanges = true)
        {
            switch (newStatus)
            {
                case PackageStatus.Available:
                    entity.Status = StagedPackageStatus.Ready;
                    break;
                case PackageStatus.FailedValidation:
                    entity.Status = StagedPackageStatus.FailedValidation;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newStatus));
            }

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }
        }

        public Task UpdateMetadataAsync(StagedPackage entity, object metadata, bool commitChanges = true)
        {
            var packageMetadata = metadata as PackageStreamMetadata;
            if (packageMetadata == null)
            {
                return Task.CompletedTask;
            }

            return _packageService.UpdatePackageStreamMetadataAsync(entity.Package, packageMetadata, commitChanges);
        }

        private IQueryable<StagedPackage> GetAll()
        {
            return _entitiesContext.StagedPackages
                .Include(candidate => candidate.Package.PackageRegistration);
        }
    }
}
