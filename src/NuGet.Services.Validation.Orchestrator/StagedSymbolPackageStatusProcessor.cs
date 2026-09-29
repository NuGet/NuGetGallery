// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Applies only the current staged symbol validation outcome to its attempt.
    /// </summary>
    public class StagedSymbolPackageStatusProcessor : IStatusProcessor<StagedSymbolPackage>
    {
        private readonly IEntityService<StagedSymbolPackage> _entityService;

        public StagedSymbolPackageStatusProcessor(IEntityService<StagedSymbolPackage> entityService)
        {
            _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
        }

        public Task SetStatusAsync(IValidatingEntity<StagedSymbolPackage> validatingEntity, PackageValidationSet validationSet, PackageStatus status)
        {
            if (validatingEntity == null)
            {
                throw new ArgumentNullException(nameof(validatingEntity));
            }

            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            var attempt = validatingEntity.EntityRecord;
            if (!CanApplyOutcome(attempt, validationSet))
            {
                return Task.CompletedTask;
            }

            return _entityService.UpdateStatusAsync(attempt, status);
        }

        private static bool CanApplyOutcome(StagedSymbolPackage attempt, PackageValidationSet validationSet)
        {
            var matchesAttempt =
                validationSet.ValidatingType == ValidatingType.StagedSymbolPackage &&
                validationSet.PackageKey == attempt.Key &&
                validationSet.PackageETag == attempt.UploadedBlobETag;
            var isCurrentAttempt = attempt.Status == StagedPackageStatus.Validating && attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key;
            var parentIsAvailable = attempt.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Available;

            return matchesAttempt && isCurrentAttempt && parentIsAvailable;
        }
    }
}
