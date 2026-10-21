// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            var attempt = await StagedSymbolPackageRenewal.RenewAsync(identity, status, repository);
            if (attempt == null)
            {
                return;
            }

            if (status == StagedPackageStatus.Validating)
            {
                attempt.Status = await emitter.StartValidationAsync(attempt);
                await repository.CommitChangesAsync();
            }
        }
    }
}
