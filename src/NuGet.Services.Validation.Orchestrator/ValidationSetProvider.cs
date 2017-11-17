// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationSetProvider : IValidationSetProvider
    {
        private readonly IValidationStorageService _validationStorageService;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ILogger<ValidationSetProvider> _logger;

        public ValidationSetProvider(
            IValidationStorageService validationStorageService,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            ILogger<ValidationSetProvider> logger)
        {
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            if (validationConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            }
            _validationConfiguration = validationConfigurationAccessor.Value ?? throw new ArgumentException($"The Value property cannot be null", nameof(validationConfigurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PackageValidationSet> GetOrCreateValidationSetAsync(Guid validationTrackingId, Package package)
        {
            var validationSet = await _validationStorageService.GetValidationSetAsync(validationTrackingId);

            if (validationSet == null)
            {
                validationSet = await CreateValidationSet(validationTrackingId, package);
            }
            else
            {
                var sameId = package.PackageRegistration.Id.Equals(validationSet.PackageId, StringComparison.InvariantCultureIgnoreCase);
                var sameVersion = package.NormalizedVersion.Equals(validationSet.PackageNormalizedVersion, StringComparison.InvariantCultureIgnoreCase);
                if (!sameId || !sameVersion)
                {
                    throw new Exception($"Validation set package identity ({validationSet.PackageId} {validationSet.PackageNormalizedVersion})" +
                        $"does not match expected package identity ({package.PackageRegistration.Id} {package.NormalizedVersion})");
                }
            }

            return validationSet;
        }

        private async Task<PackageValidationSet> CreateValidationSet(Guid validationTrackingId, Package package)
        {
            _logger.LogInformation("Creating validation set {ValidationSetId} for package {PackageId} {PackageVersion}",
                validationTrackingId,
                package.PackageRegistration.Id,
                package.NormalizedVersion);

            PackageValidationSet validationSet;
            var packageValidations = new List<PackageValidation>();
            var now = DateTime.UtcNow;
            validationSet = new PackageValidationSet
            {
                Created = now,
                PackageId = package.PackageRegistration.Id,
                PackageNormalizedVersion = package.NormalizedVersion,
                PackageKey = package.Key,
                PackageValidations = packageValidations,
                Updated = now,
                ValidationTrackingId = validationTrackingId,
            };

            foreach (var validation in _validationConfiguration.Validations)
            {
                var packageValidation = new PackageValidation
                {
                    PackageValidationSet = validationSet,
                    ValidationStatus = ValidationStatus.NotStarted,
                    Type = validation.Name,
                    ValidationStatusTimestamp = now,
                };

                packageValidations.Add(packageValidation);
            }

            return await _validationStorageService.CreateValidationSetAsync(validationSet);
        }
    }
}
