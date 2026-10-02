// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <inheritdoc />
    public class StagingBlobCleanupService : IStagingBlobCleanupService
    {
        private readonly IEntitiesContext _entities;

        public StagingBlobCleanupService(IEntitiesContext entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public void QueuePackageFiles(int stagedPackageIdentityKey)
        {
            ValidateIdentityKey(stagedPackageIdentityKey);

            var attempts = _entities.StagedPackages.AsNoTracking()
                .Where(attempt => attempt.StagedPackageIdentityKey == stagedPackageIdentityKey)
                .ToList();
            foreach (var attempt in attempts)
            {
                QueueFile(stagedPackageIdentityKey, attempt.UploadedBlobPath, attempt.UploadedBlobETag);
                if (attempt.ValidatedBlobPath != null || attempt.ValidatedBlobETag != null)
                {
                    QueueFile(stagedPackageIdentityKey, attempt.ValidatedBlobPath, attempt.ValidatedBlobETag);
                }
            }
        }

        public void QueueSymbolFiles(int stagedPackageIdentityKey)
        {
            ValidateIdentityKey(stagedPackageIdentityKey);

            var attempts = _entities.StagedSymbolPackages.AsNoTracking()
                .Where(attempt => attempt.StagedPackageIdentityKey == stagedPackageIdentityKey)
                .ToList();
            foreach (var attempt in attempts)
            {
                QueueFile(stagedPackageIdentityKey, attempt.UploadedBlobPath, attempt.UploadedBlobETag);
            }
        }

        public bool HasLiveReference(StagingBlobCleanup cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            ValidateIdentityKey(cleanup.StagedPackageIdentityKey);
            if (string.IsNullOrWhiteSpace(cleanup.BlobPath) || string.IsNullOrWhiteSpace(cleanup.BlobETag))
            {
                throw new ArgumentException("The staging file reference is incomplete.", nameof(cleanup));
            }

            var packages = _entities.StagedPackages.Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Succeeded);
            if (packages.Any(attempt => attempt.UploadedBlobPath == cleanup.BlobPath || attempt.ValidatedBlobPath == cleanup.BlobPath))
            {
                return true;
            }

            var symbols = _entities.StagedSymbolPackages.Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Succeeded);
            if (symbols.Any(attempt => attempt.UploadedBlobPath == cleanup.BlobPath))
            {
                return true;
            }

            return cleanup.BlobPath.EndsWith(CoreConstants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase)
                && symbols.Any(attempt => attempt.StagedPackageIdentityKey == cleanup.StagedPackageIdentityKey && attempt.Status == StagedPackageStatus.Validating);
        }

        private void QueueFile(int identityKey, string path, string etag)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(etag))
            {
                throw new InvalidOperationException($"The staging file reference for identity '{identityKey}' is incomplete.");
            }

            _entities.Set<StagingBlobCleanup>().Add(new StagingBlobCleanup
            {
                StagedPackageIdentityKey = identityKey,
                BlobPath = path,
                BlobETag = etag,
                QueuedDate = DateTime.UtcNow,
            });
        }

        private static void ValidateIdentityKey(int identityKey)
        {
            if (identityKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(identityKey));
            }
        }
    }
}
