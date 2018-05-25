﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.ScanAndSign)]
    public class ScanAndSignProcessor : IProcessor
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICorePackageService _packageService;
        private readonly IPackageCriteriaEvaluator _criteriaEvaluator;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ScanAndSignConfiguration _configuration;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public ScanAndSignProcessor(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            ICorePackageService packageService,
            IPackageCriteriaEvaluator criteriaEvaluator,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor,
            ILogger<ScanAndSignProcessor> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _criteriaEvaluator = criteriaEvaluator ?? throw new ArgumentNullException(nameof(criteriaEvaluator));
            _scanAndSignEnqueuer = scanAndSignEnqueuer ?? throw new ArgumentNullException(nameof(scanAndSignEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));

            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));

            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }

            _configuration = configurationAccessor.Value;
        }

        public async Task CleanUpAsync(IValidationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (validatorStatus.NupkgUrl == null)
            {
                return;
            }

            _logger.LogInformation(
                "Cleaning up the .nupkg URL for validation ID {ValidationId} ({PackageId} {PackageVersion}).",
                request.ValidationId,
                request.PackageId,
                request.PackageVersion);

            var blob = _blobProvider.GetBlobFromUrl(validatorStatus.NupkgUrl);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            return validatorStatus.ToValidationResult();
        }

        public async Task<IValidationResult> StartAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Scan and Sign validation with validation Id {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return validatorStatus.ToValidationResult();
            }

            if (await ShouldRepositorySignAsync(request))
            {
                var owners = FindPackageOwners(request);

                _logger.LogInformation(
                    "Repository signing {PackageId} {PackageVersion} with {ServiceIndex} and {Owners}",
                    request.PackageId,
                    request.PackageVersion,
                    _configuration.V3ServiceIndexUrl,
                    owners);

                await _scanAndSignEnqueuer.EnqueueScanAndSignAsync(request, _configuration.V3ServiceIndexUrl, owners);
            }
            else
            {
                if (ShouldSkipScan(request))
                {
                    return ValidationResult.Succeeded;
                }

                await _scanAndSignEnqueuer.EnqueueScanAsync(request);
            }

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return result.ToValidationResult();
        }

        private bool ShouldSkipScan(IValidationRequest request)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(
                request.PackageId,
                request.PackageVersion);

            if (!_criteriaEvaluator.IsMatch(_configuration.PackageCriteria, package))
            {
                _logger.LogInformation(
                    "The scan for {ValidationId} ({PackageId} {PackageVersion}) was skipped due to package criteria configuration.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return true;
            }

            return false;
        }

        private async Task<bool> ShouldRepositorySignAsync(IValidationRequest request)
        {
            if (!_configuration.RepositorySigningEnabled)
            {
                _logger.LogInformation("Repository signing is disabed. Scanning instead of signing package");

                return false;
            }

            var hasRepositorySignature = await _validationContext
                .PackageSignatures
                .Where(s => s.PackageKey == request.PackageKey)
                .Where(s => s.Type == PackageSignatureType.Repository)
                .AnyAsync();

            if (hasRepositorySignature)
            {
                _logger.LogInformation(
                    "Package {PackageId} {PackageVersion} already has a repository signature. Scanning instead of signing package",
                    request.PackageId,
                    request.PackageVersion);

                return false;
            }

           return true;
        }

        private List<string> FindPackageOwners(IValidationRequest request)
        {
            var registration = _packageService.FindPackageRegistrationById(request.PackageId);

            if (registration == null)
            {
                _logger.LogError("Attempted to validate package that has no package registration");

                throw new InvalidOperationException($"Registration for package id {request.PackageId} does not exist");
            }

            return registration
                .Owners
                .Select(o => o.Username)
                .ToList();
        }
    }
}
