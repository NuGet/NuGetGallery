// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class ValidationService : IValidationService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly IPackageService _packageService;
        private readonly IPackageValidationInitiator _initiator;
        private readonly IEntityRepository<PackageValidationSet> _validationSets;
        private readonly ITelemetryService _telemetryService;

        public ValidationService(
            IAppConfiguration appConfiguration,
            IPackageService packageService,
            IPackageValidationInitiator initiator,
            ITelemetryService telemetryService,
            IEntityRepository<PackageValidationSet> validationSets = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _initiator = initiator ?? throw new ArgumentNullException(nameof(initiator));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));

            _validationSets = validationSets;

            // Validation database should not be accessed when async validation is disabled. Features
            // which depend on the database should be behind this feature flag.
            if (_appConfiguration.AsynchronousPackageValidationEnabled && _validationSets == null)
            {
                throw new ArgumentNullException(nameof(validationSets));
            }
        }

        public async Task StartValidationAsync(Package package)
        {
            var packageStatus = await _initiator.StartValidationAsync(package);

            await _packageService.UpdatePackageStatusAsync(
                package,
                packageStatus,
                commitChanges: false);
        }

        public async Task RevalidateAsync(Package package)
        {
            await _initiator.StartValidationAsync(package);

            _telemetryService.TrackPackageRevalidate(package);
        }

        public bool IsValidatingTooLong(Package package)
        {
            if (package.PackageStatusKey == PackageStatus.Validating)
            {
                return ((DateTime.UtcNow - package.Created) >= _appConfiguration.ValidationExpectedTime);
            }

            return false;
        }

        public IReadOnlyList<ValidationIssue> GetLatestValidationIssues(Package package)
        {
            IReadOnlyList<ValidationIssue> issues = new ValidationIssue[0];

            // Only query the database for validation issues if the package has failed validation.
            if (package.PackageStatusKey == PackageStatus.FailedValidation)
            {
                // Grab the most recently completed validation set for this package. Note that the orchestrator will stop
                // processing a validation set if all validation succeed, OR, one or more validation fails.
                var validationSet = _validationSets
                                        .GetAll()
                                        .Where(s => s.PackageKey == package.Key)
                                        .Where(s => s.PackageValidations.All(v => v.ValidationStatus == ValidationStatus.Succeeded) ||
                                                    s.PackageValidations.Any(v => v.ValidationStatus == ValidationStatus.Failed))
                                        .Include(s => s.PackageValidations.Select(v => v.PackageValidationIssues))
                                        .OrderByDescending(s => s.Updated)
                                        .FirstOrDefault();

                if (validationSet != null)
                {
                    issues = validationSet.GetValidationIssues();
                }

                // If the package failed validation but we could not find an issue that explains why, use a generic error message.
                if (issues == null || !issues.Any())
                {
                    issues = new[] { ValidationIssue.Unknown };
                }
            }

            return issues;
        }
    }
}