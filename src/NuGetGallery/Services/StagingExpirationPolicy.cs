// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Shares staging deadlines, hard-cutoff checks, and owner-initiated deadline refresh rules.
    /// </summary>
    internal static class StagingExpirationPolicy
    {
        internal static DateTime CreateDeadline(IAppConfiguration configuration)
        {
            if (configuration.StagingExpirationDays <= 0)
            {
                throw new InvalidOperationException("StagingExpirationDays must be positive.");
            }

            return DateTime.UtcNow.AddDays(configuration.StagingExpirationDays);
        }

        internal static DateTime GetDeadline(StagedPackage attempt)
        {
            return attempt.StagedPackageIdentity.StagingGroup?.ExpirationDate ?? attempt.ExpirationDate;
        }

        internal static DateTime GetDeadline(StagedSymbolPackage attempt)
        {
            return attempt.StagedPackageIdentity.StagingGroup?.ExpirationDate ?? attempt.ExpirationDate;
        }

        internal static bool HasExpired(StagingGroup group)
        {
            return group != null && !group.ActivePromotionId.HasValue && group.ExpirationDate <= DateTime.UtcNow;
        }

        internal static bool HasExpired(StagedPackage attempt)
        {
            return attempt.Status != StagedPackageStatus.Promoting
                && attempt.StagedPackageIdentity.StagingGroup?.ActivePromotionId.HasValue != true
                && GetDeadline(attempt) <= DateTime.UtcNow;
        }

        internal static bool HasExpired(StagedSymbolPackage attempt)
        {
            return attempt.Status != StagedPackageStatus.Promoting
                && attempt.StagedPackageIdentity.StagingGroup?.ActivePromotionId.HasValue != true
                && GetDeadline(attempt) <= DateTime.UtcNow;
        }

        internal static void EnsureMutable(StagedPackageIdentity identity, StagingGroup destination = null, bool includePackage = true, bool includeSymbols = true)
        {
            if (HasExpired(identity.StagingGroup) || HasExpired(destination))
            {
                throw new StagingExpiredException();
            }

            if (includePackage)
            {
                var package = identity.CurrentStagedPackage;
                if (package != null && package.Status != StagedPackageStatus.Deleted)
                {
                    if (identity.Package.PackageStatusKey == PackageStatus.Staged && HasExpired(package))
                    {
                        throw new StagingExpiredException();
                    }
                }
            }

            if (includeSymbols)
            {
                var symbols = identity.CurrentStagedSymbolPackage;
                if (symbols != null && symbols.Status != StagedPackageStatus.Succeeded && HasExpired(symbols))
                {
                    throw new StagingExpiredException();
                }
            }
        }

        internal static void RefreshGroup(StagingGroup group, DateTime deadline)
        {
            if (group != null && !HasExpired(group))
            {
                group.ExpirationDate = deadline;
            }
        }
    }

    /// <summary>
    /// Signals that an owner mutation would revive logically expired staging.
    /// </summary>
    internal class StagingExpiredException : InvalidOperationException
    {
        internal StagingExpiredException() : base("This staging has expired. Delete the expired staging before uploading or moving new content.")
        {
        }
    }
}
