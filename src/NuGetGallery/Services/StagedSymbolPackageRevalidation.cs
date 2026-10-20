// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Creates fresh attempts for retained symbols within the caller's parent-mutation transaction.
    /// </summary>
    internal static class StagedSymbolPackageRevalidation
    {
        internal static async Task RenewAsync(
            StagedPackageIdentity identity,
            StagedPackageStatus status,
            IEntityRepository<StagedSymbolPackage> repository,
            IStagedSymbolPackageValidationMessageEmitter emitter)
        {
            if (!identity.CurrentStagedSymbolPackageKey.HasValue)
            {
                return;
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

            if (status == StagedPackageStatus.Validating)
            {
                attempt.Status = await emitter.StartValidationAsync(attempt);
                await repository.CommitChangesAsync();
            }
        }
    }
}
