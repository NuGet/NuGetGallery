// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Staging;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Creates ingestion-only settings without changing ordinary symbol validation prerequisites.
    /// </summary>
    public static class SymbolPromotionValidationConfiguration
    {
        public static bool IsPromotion(PackageValidationSet validationSet)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            return validationSet.ValidatingType == ValidatingType.StagedSymbolPackage
                && validationSet.PackageValidations.Count == 1
                && validationSet.PackageValidations.Single().Type == ValidatorName.SymbolsIngester;
        }

        /// <summary>
        /// Checks whether a persisted ingestion set belongs to the current accepted symbol promotion.
        /// </summary>
        public static bool MatchesAttempt(PackageValidationSet validationSet, StagedSymbolPackage attempt)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            var matchesAttempt = validationSet.PackageKey == attempt.Key && validationSet.PackageETag == attempt.UploadedBlobETag;
            var isCurrentAttempt = attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key;
            var isPromotionState = attempt.Status == StagedPackageStatus.Promoting
                || attempt.Status == StagedPackageStatus.Succeeded
                || attempt.Status == StagedPackageStatus.PromotionFailed;
            var identity = attempt.StagedPackageIdentity;
            var isActiveGroup = attempt.Status != StagedPackageStatus.Promoting
                || !identity.StagingGroupKey.HasValue
                || identity.StagingGroup?.ActivePromotionId == attempt.ActivePromotionId;

            if (!IsPromotion(validationSet) || !matchesAttempt || !isCurrentAttempt || !attempt.ActivePromotionId.HasValue || !isPromotionState)
            {
                return false;
            }

            if (!isActiveGroup)
            {
                return false;
            }

            var package = attempt.StagedPackageIdentity.Package;
            var matchesPackage = string.Equals(validationSet.PackageId, package.Id, StringComparison.OrdinalIgnoreCase)
                && validationSet.PackageNormalizedVersion == package.NormalizedVersion;

            return matchesPackage
                && validationSet.ValidationTrackingId == SymbolPromotionValidationTrackingId.Create(attempt.ActivePromotionId.Value, attempt.Key);
        }

        public static ValidationConfigurationItem Create(ValidationConfiguration symbolsConfiguration)
        {
            if (symbolsConfiguration == null)
            {
                throw new ArgumentNullException(nameof(symbolsConfiguration));
            }

            if (!symbolsConfiguration.EnableStagedSymbolPromotion)
            {
                throw new NotSupportedException("Staged symbol promotion is not enabled.");
            }

            var ingester = symbolsConfiguration.Validations?.SingleOrDefault(validation => validation.Name == ValidatorName.SymbolsIngester);
            if (ingester == null)
            {
                throw new ArgumentException("Symbol promotion requires SymbolsIngester configuration.", nameof(symbolsConfiguration));
            }

            return new ValidationConfigurationItem
            {
                Name = ValidatorName.SymbolsIngester,
                ShouldStart = true,
                FailureBehavior = ValidationFailureBehavior.MustSucceed,
                TrackAfter = ingester.TrackAfter,
            };
        }
    }
}
