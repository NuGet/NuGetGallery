// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Renews retained symbols without carrying a retired attempt's validation or promotion binding.
    /// </summary>
    public static class StagedSymbolPackageRenewal
    {
        /// <summary>
        /// Creates and binds a fresh symbol attempt within the caller's parent-mutation transaction.
        /// </summary>
        public static async Task<StagedSymbolPackage> RenewAsync(
            StagedPackageIdentity identity,
            StagedPackageStatus status,
            IEntityRepository<StagedSymbolPackage> repository)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (!identity.CurrentStagedSymbolPackageKey.HasValue)
            {
                return null;
            }

            var previous = identity.CurrentStagedSymbolPackage ?? throw new InvalidOperationException("The current staged symbol attempt was not loaded.");
            var attempt = new StagedSymbolPackage
            {
                StagedPackageIdentity = identity,
                SymbolPackage = previous.SymbolPackage,
                UploadedBlobPath = previous.UploadedBlobPath,
                UploadedBlobETag = previous.UploadedBlobETag,
                UploadedDate = previous.UploadedDate,
                ExpirationDate = previous.ExpirationDate,
                Status = status,
            };

            previous.Status = StagedPackageStatus.Superseded;
            repository.InsertOnCommit(attempt);
            await repository.CommitChangesAsync();

            identity.CurrentStagedSymbolPackageKey = attempt.Key;
            identity.CurrentStagedSymbolPackage = attempt;
            await repository.CommitChangesAsync();
            return attempt;
        }
    }
}
