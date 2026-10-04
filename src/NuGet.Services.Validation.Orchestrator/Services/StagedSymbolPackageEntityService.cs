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
    /// Finds staged symbol attempts and updates their validation status.
    /// </summary>
    public class StagedSymbolPackageEntityService : IEntityService<StagedSymbolPackage>
    {
        private readonly IEntitiesContext _entitiesContext;

        public StagedSymbolPackageEntityService(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IValidatingEntity<StagedSymbolPackage> FindPackageByIdAndVersionStrict(string id, string version)
        {
            var entity = GetAll().FirstOrDefault(candidate =>
                candidate.StagedPackageIdentity.Package.PackageRegistration.Id == id &&
                candidate.StagedPackageIdentity.Package.NormalizedVersion == version &&
                (candidate.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Available || candidate.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged) &&
                candidate.StagedPackageIdentity.CurrentStagedSymbolPackageKey == candidate.Key);
            return entity == null ? null : new StagedSymbolPackageValidatingEntity(entity);
        }

        public IValidatingEntity<StagedSymbolPackage> FindPackageByKey(int key)
        {
            var entity = GetAll().SingleOrDefault(candidate => candidate.Key == key);
            return entity == null ? null : new StagedSymbolPackageValidatingEntity(entity);
        }

        public async Task UpdateStatusAsync(StagedSymbolPackage entity, PackageStatus status, bool commitChanges = true)
        {
            switch (status)
            {
                case PackageStatus.Available:
                    entity.Status = StagedPackageStatus.Ready;
                    break;
                case PackageStatus.FailedValidation:
                    entity.Status = StagedPackageStatus.FailedValidation;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (commitChanges)
            {
                await _entitiesContext.SaveChangesAsync();
            }
        }

        public Task UpdateMetadataAsync(StagedSymbolPackage entity, object metadata, bool commitChanges = true)
        {
            return Task.CompletedTask;
        }

        private IQueryable<StagedSymbolPackage> GetAll()
        {
            return _entitiesContext.StagedSymbolPackages
                .Include(candidate => candidate.StagedPackageIdentity.Owner)
                .Include(candidate => candidate.StagedPackageIdentity.Package.PackageRegistration.Owners)
                .Include(candidate => candidate.StagedPackageIdentity.CurrentStagedPackage)
                .Include(candidate => candidate.StagedPackageIdentity.StagingGroup)
                .Include(candidate => candidate.SymbolPackage);
        }
    }
}
