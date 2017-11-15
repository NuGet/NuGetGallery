// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides an access layer to the validation information stored in DB
    /// </summary>
    public class ValidationStorageService : IValidationStorageService
    {
        private readonly ValidationEntitiesContext _validationContext;
        private readonly ILogger<ValidationStorageService> _logger;

        public ValidationStorageService(ValidationEntitiesContext validationContext, ILogger<ValidationStorageService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageValidationSet> GetValidationSetAsync(Guid validationTrackingId)
        {
            return await _validationContext
                .PackageValidationSets
                .Include(pvs => pvs.PackageValidations)
                .FirstOrDefaultAsync(vs => vs.ValidationTrackingId == validationTrackingId);
        }

        public async Task<PackageValidationSet> CreateValidationSetAsync(PackageValidationSet packageValidationSet)
        {
            packageValidationSet = packageValidationSet ?? throw new ArgumentNullException(nameof(packageValidationSet));
            _logger.LogInformation("Adding validation set entry to DB, {ValidationSetId} {PackageId} {PackageVersion}",
                packageValidationSet.ValidationTrackingId,
                packageValidationSet.PackageId,
                packageValidationSet.PackageNormalizedVersion);
            foreach (var validation in packageValidationSet.PackageValidations)
            {
                _validationContext.PackageValidations.Add(validation);
            }
            _validationContext.PackageValidationSets.Add(packageValidationSet);
            await _validationContext.SaveChangesAsync();
            return await GetValidationSetAsync(packageValidationSet.ValidationTrackingId);
        }

        public async Task MarkValidationStartedAsync(PackageValidation packageValidation, ValidationStatus startedStatus)
        {
            packageValidation = packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
            _logger.LogInformation("Marking validation {ValidationName} {ValidationId} {PackageId} {PackageVersion} as started with status {ValidationStatus}",
                packageValidation.Type,
                packageValidation.PackageValidationSet.ValidationTrackingId,
                packageValidation.PackageValidationSet.PackageId,
                packageValidation.PackageValidationSet.PackageNormalizedVersion,
                startedStatus);
            if (startedStatus == ValidationStatus.NotStarted)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startedStatus),
                    $"Cannot mark validation {packageValidation.Type} for " +
                    $"{packageValidation.PackageValidationSet.PackageId} " +
                    $"{packageValidation.PackageValidationSet.PackageNormalizedVersion} as started " +
                    $"with status {ValidationStatus.NotStarted}");
            }

            packageValidation.ValidationStatus = startedStatus;
            var now = DateTime.UtcNow;
            packageValidation.ValidationStatusTimestamp = now;
            packageValidation.Started = now;
            await _validationContext.SaveChangesAsync();
        }

        public async Task UpdateValidationStatusAsync(PackageValidation packageValidation, ValidationStatus validationStatus)
        {
            packageValidation = packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
            _logger.LogInformation("Updating the status of the validation {ValidationName} {ValidationId} {PackageId} {PackageVersion} to {ValidationStatus}",
                packageValidation.Type,
                packageValidation.PackageValidationSet.ValidationTrackingId,
                packageValidation.PackageValidationSet.PackageId,
                packageValidation.PackageValidationSet.PackageNormalizedVersion,
                validationStatus);
            if (packageValidation.ValidationStatus == validationStatus)
            {
                return;
            }

            packageValidation.ValidationStatus = validationStatus;
            packageValidation.ValidationStatusTimestamp = DateTime.UtcNow;
            await _validationContext.SaveChangesAsync();
        }
    }
}
