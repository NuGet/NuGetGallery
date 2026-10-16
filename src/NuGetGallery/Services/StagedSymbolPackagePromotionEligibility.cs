// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Shares symbol-promotion eligibility between acceptance, owner controls, and API status.
    /// </summary>
    internal static class StagedSymbolPackagePromotionEligibility
    {
        internal static IReadOnlyList<StagingBlockerResponse> GetBlockers(StagedSymbolPackage attempt, StagedPackage parentAttempt = null, bool forGroup = false)
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

            if (identity.StagingGroupKey.HasValue && !forGroup)
            {
                blockers.Add(new StagingBlockerResponse("GroupPromotionRequired", "Promote these symbols with their staging group."));
            }

            if (identity.CurrentStagedSymbolPackageKey != attempt.Key || attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
            {
                blockers.Add(new StagingBlockerResponse("SymbolsChanged", "The staged symbols changed. Refresh to check their status."));
            }

            var includesStagedParent = parentAttempt != null
                && parentAttempt.StagedPackageIdentity.Key == identity.Key
                && identity.CurrentStagedPackageKey == parentAttempt.Key
                && parentAttempt.Status == StagedPackageStatus.Ready
                && identity.Package.PackageStatusKey == PackageStatus.Staged;
            if (identity.Package.PackageStatusKey != PackageStatus.Available && !includesStagedParent)
            {
                blockers.Add(new StagingBlockerResponse("ParentPackageNotAvailable", "Publish the parent package before promoting these symbols."));
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
            if (identity.StagingGroupKey.HasValue || identity.Package.PackageStatusKey != PackageStatus.Available)
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
