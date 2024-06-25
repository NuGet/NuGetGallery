// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class ValidationService : IValidationService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly IPackageService _packageService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly IPackageValidationInitiator<Package> _packageValidationInitiator;
        private readonly IPackageValidationInitiator<SymbolPackage> _symbolPackageValidationInitiator;
        private readonly IEntityRepository<PackageValidationSet> _validationSets;
        private readonly ITelemetryService _telemetryService;

        public ValidationService(
            IAppConfiguration appConfiguration,
            IPackageService packageService,
            IPackageValidationInitiator<Package> packageValidationInitiator,
            IPackageValidationInitiator<SymbolPackage> symbolPackageValidationInitiator,
            ITelemetryService telemetryService,
            ISymbolPackageService symbolPackageService,
            IEntityRepository<PackageValidationSet> validationSets = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageValidationInitiator = packageValidationInitiator ?? throw new ArgumentNullException(nameof(packageValidationInitiator));
            _symbolPackageValidationInitiator = symbolPackageValidationInitiator ?? throw new ArgumentNullException(nameof(symbolPackageValidationInitiator));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));

            _validationSets = validationSets;

            // Validation database should not be accessed when async validation is disabled. Features
            // which depend on the database should be behind this feature flag.
            if (_appConfiguration.AsynchronousPackageValidationEnabled && _validationSets == null)
            {
                throw new ArgumentNullException(nameof(validationSets));
            }
        }

        public async Task UpdatePackageAsync(Package package)
        {
            var packageStatus = _packageValidationInitiator.GetPackageStatus(package);

            await UpdatePackageInternalAsync(package, packageStatus);
        }

        public async Task UpdatePackageAsync(SymbolPackage symbolPackage)
        {
            var symbolPackageStatus = _symbolPackageValidationInitiator.GetPackageStatus(symbolPackage);

            await UpdateSymbolPackageInternalAsync(symbolPackage, symbolPackageStatus);
        }

        public async Task StartValidationAsync(Package package)
        {
            var packageStatus = await _packageValidationInitiator.StartValidationAsync(package);

            await UpdatePackageInternalAsync(package, packageStatus);
        }

        public async Task RevalidateAsync(Package package)
        {
            await _packageValidationInitiator.StartValidationAsync(package);

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

        public IReadOnlyList<ValidationIssue> GetLatestPackageValidationIssues(Package package)
        {
            return GetValidationIssues(package.Key, package.PackageStatusKey, ValidatingType.Package);
        }

        public IReadOnlyList<ValidationIssue> GetLatestPackageValidationIssues(SymbolPackage symbolPackage)
        {
            if (symbolPackage == null)
            {
                return new List<ValidationIssue>();
            }

            return GetValidationIssues(symbolPackage.Key, symbolPackage.StatusKey, ValidatingType.SymbolPackage);
        }

        public async Task StartValidationAsync(SymbolPackage symbolPackage)
        {
            var symbolPackageStatus = await _symbolPackageValidationInitiator.StartValidationAsync(symbolPackage);
            await UpdateSymbolPackageInternalAsync(symbolPackage, symbolPackageStatus);
        }

        public async Task RevalidateAsync(SymbolPackage symbolPackage)
        {
            await _symbolPackageValidationInitiator.StartValidationAsync(symbolPackage);

            _telemetryService.TrackSymbolPackageRevalidate(symbolPackage.Id, symbolPackage.Version);
        }

        private async Task UpdatePackageInternalAsync(Package package, PackageStatus packageStatus)
        {
            await _packageService.UpdatePackageStatusAsync(
                package,
                packageStatus,
                commitChanges: false);
        }

        private async Task UpdateSymbolPackageInternalAsync(SymbolPackage symbolPackage, PackageStatus symbolPackageStatus)
        {
            await _symbolPackageService.UpdateStatusAsync(symbolPackage,
                symbolPackageStatus,
                commitChanges: false);
        }

        private IReadOnlyList<ValidationIssue> GetValidationIssues(int entityKey, PackageStatus status, ValidatingType validatingType)
        {
            IReadOnlyList<ValidationIssue> issues = Array.Empty<ValidationIssue>();

            // Only query the database for validation issues if the package has failed validation.
            if (status == PackageStatus.FailedValidation)
            {
                // Grab the most recently completed validation set for this package. Note that the orchestrator will stop
                // processing a validation set if all validation succeed, OR, one or more validation fails.
                var validationSet = _validationSets?
                    .GetAll()
                    .Where(s => s.PackageKey == entityKey && s.ValidatingType == validatingType)
                    .Where(s => s.PackageValidations.All(v => v.ValidationStatus == ValidationStatus.Succeeded) ||
                                s.PackageValidations.Any(v => v.ValidationStatus == ValidationStatus.Failed))
                    .Include(s => s.PackageValidations.Select(v => v.PackageValidationIssues))
                    .OrderByDescending(s => s.Updated)
                    .FirstOrDefault();

                if (validationSet != null)
                {
                    issues = validationSet.GetValidationIssues();
                }
            }

            return issues;
        }
    }
}