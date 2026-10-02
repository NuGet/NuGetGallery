// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<HashSet<string>> GetLiveReferencedPathsAsync(IReadOnlyCollection<StagingBlobCleanup> cleanups)
        {
            if (cleanups == null)
            {
                throw new ArgumentNullException(nameof(cleanups));
            }

            foreach (var cleanup in cleanups)
            {
                if (cleanup == null)
                {
                    throw new ArgumentException("The cleanup collection contains a null request.", nameof(cleanups));
                }

                ValidateIdentityKey(cleanup.StagedPackageIdentityKey);
                if (string.IsNullOrWhiteSpace(cleanup.BlobPath) || string.IsNullOrWhiteSpace(cleanup.BlobETag))
                {
                    throw new ArgumentException("The staging file reference is incomplete.", nameof(cleanups));
                }
            }

            var livePaths = new HashSet<string>(StringComparer.Ordinal);
            if (cleanups.Count == 0)
            {
                return livePaths;
            }

            var paths = cleanups.Select(cleanup => cleanup.BlobPath).Distinct().ToList();
            var parentIdentityKeys = cleanups.Where(cleanup => cleanup.BlobPath.EndsWith(CoreConstants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase))
                .Select(cleanup => cleanup.StagedPackageIdentityKey).Distinct().ToList();
            var packages = await _entities.StagedPackages
                .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Succeeded)
                .Where(attempt => paths.Contains(attempt.UploadedBlobPath) || paths.Contains(attempt.ValidatedBlobPath))
                .Select(attempt => new { attempt.UploadedBlobPath, attempt.ValidatedBlobPath })
                .ToListAsync();
            foreach (var package in packages)
            {
                livePaths.Add(package.UploadedBlobPath);
                if (package.ValidatedBlobPath != null)
                {
                    livePaths.Add(package.ValidatedBlobPath);
                }
            }

            var symbols = await _entities.StagedSymbolPackages
                .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Succeeded)
                .Where(attempt => paths.Contains(attempt.UploadedBlobPath)
                    || (attempt.Status == StagedPackageStatus.Validating && parentIdentityKeys.Contains(attempt.StagedPackageIdentityKey)))
                .Select(attempt => new { attempt.UploadedBlobPath, attempt.StagedPackageIdentityKey, attempt.Status })
                .ToListAsync();
            livePaths.UnionWith(symbols.Select(attempt => attempt.UploadedBlobPath));
            var validatingIdentityKeys = new HashSet<int>(symbols.Where(attempt => attempt.Status == StagedPackageStatus.Validating).Select(attempt => attempt.StagedPackageIdentityKey));
            foreach (var cleanup in cleanups)
            {
                if (cleanup.BlobPath.EndsWith(CoreConstants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase) && validatingIdentityKeys.Contains(cleanup.StagedPackageIdentityKey))
                {
                    livePaths.Add(cleanup.BlobPath);
                }
            }

            livePaths.IntersectWith(paths);
            return livePaths;
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
