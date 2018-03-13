// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Issues;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides an access layer to the validation information stored in DB
    /// </summary>
    public class ValidationStorageService : IValidationStorageService
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationStorageService> _logger;

        public ValidationStorageService(
            IValidationEntitiesContext validationContext,
            ITelemetryService telemetryService,
            ILogger<ValidationStorageService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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

            TrackValidationStatus(packageValidation);
        }

        public Task UpdateValidationSetAsync(PackageValidationSet packageValidationSet)
        {
            packageValidationSet = packageValidationSet ?? throw new ArgumentNullException(nameof(packageValidationSet));

            _logger.LogInformation("Updating the status of the validation set {ValidationId} {PackageId} {PackageVersion}",
                packageValidationSet.ValidationTrackingId,
                packageValidationSet.PackageId,
                packageValidationSet.PackageNormalizedVersion);

            packageValidationSet.Updated = DateTime.UtcNow;

            return _validationContext.SaveChangesAsync();
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

            var previousValidationStatus = packageValidation.ValidationStatus;

            AddValidationIssues(packageValidation, validationResult.Issues);

            packageValidation.ValidationStatus = validationResult.Status;
            packageValidation.ValidationStatusTimestamp = DateTime.UtcNow;
            await _validationContext.SaveChangesAsync();

            TrackValidationStatus(packageValidation);
        }

        private void TrackValidationStatus(PackageValidation packageValidation)
        {
            if (packageValidation.ValidationStatus != ValidationStatus.Failed
                && packageValidation.ValidationStatus != ValidationStatus.Succeeded)
            {
                return;
            }

            var isSuccess = packageValidation.ValidationStatus == ValidationStatus.Succeeded;

            TimeSpan validatorDuration = TimeSpan.Zero;
            if (packageValidation.Started.HasValue)
            {
                validatorDuration = packageValidation.ValidationStatusTimestamp - packageValidation.Started.Value;
            }

            _telemetryService.TrackValidatorDuration(
               validatorDuration,
               packageValidation.Type,
               isSuccess);

            var issues = (packageValidation.PackageValidationIssues ?? Enumerable.Empty<PackageValidationIssue>()).ToList();
            _telemetryService.TrackValidationIssueCount(
                issues.Count,
                packageValidation.Type,
                isSuccess);

            foreach (var issue in issues)
            {
                _telemetryService.TrackValidationIssue(packageValidation.Type, issue.IssueCode);

                var deserializedIssue = ValidationIssue.Deserialize(issue.IssueCode, issue.Data);
                if (issue.IssueCode == ValidationIssueCode.ClientSigningVerificationFailure
                    && deserializedIssue is ClientSigningVerificationFailure typedIssue)
                {
                    _telemetryService.TrackClientValidationIssue(packageValidation.Type, typedIssue.ClientCode);
                }
            }
        }

        public async Task<bool> OtherRecentValidationSetForPackageExists(
            int packageKey,
            TimeSpan recentDuration,
            Guid currentValidationSetTrackingId)
        {
            var cutoffTimestamp = DateTime.UtcNow - recentDuration;
            return await _validationContext
                .PackageValidationSets
                .AnyAsync(pvs => pvs.PackageKey == packageKey
                    && pvs.Created > cutoffTimestamp
                    && pvs.ValidationTrackingId != currentValidationSetTrackingId);
        }

        public async Task<int> GetValidationSetCountAsync(int packageKey)
        {
            return await _validationContext
                .PackageValidationSets
                .CountAsync(x => x.PackageKey == packageKey);
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
