// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.ScanAndSign)]
    public class ScanAndSignProcessor : INuGetProcessor
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICorePackageService _packageService;
        private readonly ICriteriaEvaluator<Package> _criteriaEvaluator;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ScanAndSignConfiguration _configuration;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public ScanAndSignProcessor(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            ICorePackageService packageService,
            ICriteriaEvaluator<Package> criteriaEvaluator,
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

        public async Task CleanUpAsync(INuGetValidationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (validatorStatus.NupkgUrl == null)
            {
                return;
            }

            if (!_configuration.RepositorySigningEnabled)
            {
                _logger.LogWarning(
                    "Skipping cleanup of .nupkg for validation ID {ValidationId} ({PackageId} {PackageVersion})",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

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

        public async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = await GetProcessorStatusAsync(request);

            return result.ToNuGetValidationResponse();
        }

        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var processorStatus = await GetProcessorStatusAsync(request);

            if (processorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Scan and Sign validation with validation Id {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return processorStatus.ToNuGetValidationResponse();
            }

            var owners = FindPackageOwners(request);

            if (await ShouldRepositorySignAsync(request))
            {
                _logger.LogInformation(
                    "Repository signing {PackageId} {PackageVersion} with {ServiceIndex} and {Owners}",
                    request.PackageId,
                    request.PackageVersion,
                    _configuration.V3ServiceIndexUrl,
                    owners);

                await _scanAndSignEnqueuer.EnqueueScanAndSignAsync(request.ValidationId, request.NupkgUrl, _configuration.V3ServiceIndexUrl, owners);
            }
            else
            {
                if (ShouldSkipScan(request))
                {
                    return NuGetValidationResponse.Succeeded;
                }

                await _scanAndSignEnqueuer.EnqueueScanAsync(request.ValidationId, request.NupkgUrl);
            }

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, processorStatus, ValidationStatus.Incomplete);

            return result.ToNuGetValidationResponse();
        }

        private async Task<ValidatorStatus> GetProcessorStatusAsync(INuGetValidationRequest request)
        {
            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (!_configuration.RepositorySigningEnabled && validatorStatus.NupkgUrl != null)
            {
                _logger.LogWarning(
                    "Suppressing .nupkg url as repository signing is disabled for {ValidationId} ({PackageId} {PackageVersion})",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                validatorStatus.NupkgUrl = null;
            }

            return validatorStatus;
        }

        private bool ShouldSkipScan(INuGetValidationRequest request)
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

        private async Task<bool> ShouldRepositorySignAsync(INuGetValidationRequest request)
        {
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

        private List<string> FindPackageOwners(INuGetValidationRequest request)
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
                .ToList()
                .OrderBy(u => u, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }
    }
}
