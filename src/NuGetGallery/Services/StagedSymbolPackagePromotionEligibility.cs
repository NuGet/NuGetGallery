// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Shares initial symbol-promotion eligibility between acceptance, owner controls, and API status.
    /// </summary>
    internal static class StagedSymbolPackagePromotionEligibility
    {
        internal static IReadOnlyList<StagingBlockerResponse> GetBlockers(StagedSymbolPackage attempt)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            var identity = attempt.StagedPackageIdentity;
            var blockers = new List<StagingBlockerResponse>();
            if (attempt.Status == StagedPackageStatus.WaitingForParent)
            {
                blockers.Add(new StagingBlockerResponse("ParentPackageMissing", "Restage the parent package to validate these symbols."));
            }
            else if (attempt.Status != StagedPackageStatus.Ready)
            {
                blockers.Add(new StagingBlockerResponse("SymbolsNotReady", "The staged symbols are not ready for promotion."));
            }

            if (identity.StagingGroupKey.HasValue)
            {
                blockers.Add(new StagingBlockerResponse("SymbolPromotionUnavailable", "Symbol promotion is not available for staging groups yet."));
            }

            if (identity.CurrentStagedSymbolPackageKey != attempt.Key || attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
            {
                blockers.Add(new StagingBlockerResponse("SymbolsChanged", "The staged symbols changed. Refresh to check their status."));
            }

            if (identity.Package.PackageStatusKey != PackageStatus.Available)
            {
                blockers.Add(new StagingBlockerResponse("ParentPackageNotAvailable", "Publish the parent package before promoting these symbols."));
            }

            if (identity.Package.SymbolPackages.Any(symbols => symbols.StatusKey == PackageStatus.Available))
            {
                blockers.Add(new StagingBlockerResponse("PublicSymbolsExist", "Replacing published symbols is not available yet."));
            }

            return blockers;
        }

        internal static bool CanResend(StagedSymbolPackage attempt)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            var identity = attempt.StagedPackageIdentity;
            if (identity.StagingGroupKey.HasValue)
            {
                return false;
            }

            if (identity.CurrentStagedSymbolPackageKey != attempt.Key || attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
            {
                return false;
            }

            if (attempt.Status != StagedPackageStatus.Promoting || !attempt.ActivePromotionId.HasValue)
            {
                return false;
            }

            // Promotions accepted before resend tracking was added have no sent date.
            if (!attempt.PromotionMessageSentDate.HasValue)
            {
                return true;
            }

            return StagingPromotionResendPolicy.IsDue(attempt.PromotionMessageSentDate);
        }
    }
}
