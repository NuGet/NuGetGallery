// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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

        public async Task MarkValidationStartedAsync(PackageValidation packageValidation, IValidationResult validationResult)
        {
            packageValidation = packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
            _logger.LogInformation("Marking validation {ValidationName} {ValidationId} {PackageId} {PackageVersion} as started with status {ValidationStatus}",
                packageValidation.Type,
                packageValidation.PackageValidationSet.ValidationTrackingId,
                packageValidation.PackageValidationSet.PackageId,
                packageValidation.PackageValidationSet.PackageNormalizedVersion,
                validationResult.Status);
            if (validationResult.Status == ValidationStatus.NotStarted)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(validationResult),
                    $"Cannot mark validation {packageValidation.Type} for " +
                    $"{packageValidation.PackageValidationSet.PackageId} " +
                    $"{packageValidation.PackageValidationSet.PackageNormalizedVersion} as started " +
                    $"with status {ValidationStatus.NotStarted}");
            }

            packageValidation.ValidationStatus = validationResult.Status;

            // If the validation has completed, save the validation issues to the package's validation.
            if (validationResult.Status != ValidationStatus.Incomplete)
            {
                AddValidationIssues(packageValidation, validationResult.Issues);
            }

            var now = DateTime.UtcNow;
            packageValidation.ValidationStatusTimestamp = now;
            packageValidation.Started = now;
            await _validationContext.SaveChangesAsync();
        }

        public async Task UpdateValidationStatusAsync(PackageValidation packageValidation, IValidationResult validationResult)
        {
            packageValidation = packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
            _logger.LogInformation("Updating the status of the validation {ValidationName} {ValidationId} {PackageId} {PackageVersion} to {ValidationStatus}",
                packageValidation.Type,
                packageValidation.PackageValidationSet.ValidationTrackingId,
                packageValidation.PackageValidationSet.PackageId,
                packageValidation.PackageValidationSet.PackageNormalizedVersion,
                validationResult.Status);
            if (packageValidation.ValidationStatus == validationResult.Status)
            {
                return;
            }

            AddValidationIssues(packageValidation, validationResult.Issues);

            packageValidation.ValidationStatus = validationResult.Status;
            packageValidation.ValidationStatusTimestamp = DateTime.UtcNow;
            await _validationContext.SaveChangesAsync();
        }

        public async Task<bool> OtherRecentValidationSetForPackageExists(
            string packageId,
            string normalizedVersion,
            TimeSpan recentDuration,
            Guid currentValidationSetTrackingId)
        {
            var cutoffTimestamp = DateTime.UtcNow - recentDuration;
            return await _validationContext
                .PackageValidationSets
                .AnyAsync(pvs => pvs.PackageId == packageId
                    && pvs.PackageNormalizedVersion == normalizedVersion
                    && pvs.Created > cutoffTimestamp
                    && pvs.ValidationTrackingId != currentValidationSetTrackingId);
        }

        private void AddValidationIssues(PackageValidation packageValidation, IReadOnlyList<IValidationIssue> validationIssues)
        {
            foreach (var validationIssue in validationIssues)
            {
                packageValidation.PackageValidationIssues.Add(new PackageValidationIssue
                {
                    IssueCode = validationIssue.IssueCode,
                    Data = validationIssue.Serialize(),
                });
            }
        }
    }
}
