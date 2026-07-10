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
        private readonly IValidationMessageEmitter<Package> _packageValidationMessageEmitter;
        private readonly IValidationMessageEmitter<SymbolPackage> _symbolPackageValidationMessageEmitter;
        private readonly IEntityRepository<PackageValidationSet> _validationSets;
        private readonly ITelemetryService _telemetryService;

        public ValidationService(
            IAppConfiguration appConfiguration,
            IPackageService packageService,
            IValidationMessageEmitter<Package> packageValidationMessageEmitter,
            IValidationMessageEmitter<SymbolPackage> symbolPackageValidationMessageEmitter,
            ITelemetryService telemetryService,
            ISymbolPackageService symbolPackageService,
            IEntityRepository<PackageValidationSet> validationSets = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageValidationMessageEmitter = packageValidationMessageEmitter ?? throw new ArgumentNullException(nameof(packageValidationMessageEmitter));
            _symbolPackageValidationMessageEmitter = symbolPackageValidationMessageEmitter ?? throw new ArgumentNullException(nameof(symbolPackageValidationMessageEmitter));
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
            var packageStatus = _packageValidationMessageEmitter.GetPackageStatus(package);

            await UpdatePackageInternalAsync(package, packageStatus);
        }

        public async Task UpdatePackageAsync(SymbolPackage symbolPackage)
        {
            var symbolPackageStatus = _symbolPackageValidationMessageEmitter.GetPackageStatus(symbolPackage);

            await UpdateSymbolPackageInternalAsync(symbolPackage, symbolPackageStatus);
        }

        public async Task StartValidationAsync(Package package)
        {
            var packageStatus = await _packageValidationMessageEmitter.StartValidationAsync(package);

            await UpdatePackageInternalAsync(package, packageStatus);
        }

        public async Task RevalidateAsync(Package package)
        {
            await _packageValidationMessageEmitter.StartValidationAsync(package);

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
            var symbolPackageStatus = await _symbolPackageValidationMessageEmitter.StartValidationAsync(symbolPackage);
            await UpdateSymbolPackageInternalAsync(symbolPackage, symbolPackageStatus);
        }

        public async Task RevalidateAsync(SymbolPackage symbolPackage)
        {
            await _symbolPackageValidationMessageEmitter.StartValidationAsync(symbolPackage);

            _telemetryService.TrackSymbolPackageRevalidate(symbolPackage.Id, symbolPackage.Version);
        }

        public async Task FailValidationAsync(Package package)
        {
            var validationTrackingIds = GetValidationTrackingIds(package.Key, ValidatingType.Package);

            PackageStatus packageStatus = package.PackageStatusKey;
            foreach (var validationTrackingId in validationTrackingIds)
            {
                packageStatus = await _packageValidationMessageEmitter.FailValidationAsync(package, validationTrackingId);
            }

            await UpdatePackageInternalAsync(package, packageStatus);
        }

        public async Task FailValidationAsync(SymbolPackage symbolPackage)
        {
            var validationTrackingIds = GetValidationTrackingIds(symbolPackage.Key, ValidatingType.SymbolPackage);

            PackageStatus symbolPackageStatus = symbolPackage.StatusKey;
            foreach (var validationTrackingId in validationTrackingIds)
            {
                symbolPackageStatus = await _symbolPackageValidationMessageEmitter.FailValidationAsync(symbolPackage, validationTrackingId);
            }

            await UpdateSymbolPackageInternalAsync(symbolPackage, symbolPackageStatus);
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

        private List<Guid> GetValidationTrackingIds(int entityKey, ValidatingType validatingType)
        {
            // When asynchronous validation is disabled the immediate message emitter is used, which ignores the
            // tracking ID and never enqueues a message, so there is no validation set to look up.
            if (_validationSets == null)
            {
                return new List<Guid> { Guid.Empty };
            }

            // The orchestrator fails an *existing* validation set by its tracking ID. A package can have many
            // validation sets over its lifetime, but only an incomplete one keeps the package in the
            // Validating state, so we target every set that has not yet completed rather than blindly
            // the latest set (which may already be in a terminal state).
            var validationTrackingIds = _validationSets
                .GetAll()
                .Where(s => s.PackageKey == entityKey && s.ValidatingType == validatingType)
                .Where(s => s.ValidationSetStatus != ValidationSetStatus.Completed)
                .OrderByDescending(s => s.Created)
                .Select(s => s.ValidationTrackingId)
                .ToList();

            if (validationTrackingIds.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No incomplete validation set was found for {validatingType} with key {entityKey}; unable to fail its validation.");
            }

            return validationTrackingIds;
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
