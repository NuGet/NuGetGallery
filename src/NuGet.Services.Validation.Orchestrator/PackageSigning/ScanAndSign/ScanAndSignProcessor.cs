// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.ScanAndSign)]
    public class ScanAndSignProcessor : IProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly ICorePackageService _packageService;
        private readonly IPackageCriteriaEvaluator _criteriaEvaluator;
        private readonly ScanAndSignConfiguration _configuration;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public ScanAndSignProcessor(
            IValidatorStateService validatorStateService,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            ICorePackageService packageService,
            IPackageCriteriaEvaluator criteriaEvaluator,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor,
            ILogger<ScanAndSignProcessor> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _scanAndSignEnqueuer = scanAndSignEnqueuer ?? throw new ArgumentNullException(nameof(scanAndSignEnqueuer));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _criteriaEvaluator = criteriaEvaluator ?? throw new ArgumentNullException(nameof(criteriaEvaluator));
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
        }

        public Task CleanUpAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // scan only for now does not require cleanup
            return Task.CompletedTask;
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (ShouldSkip(request))
            {
                return ValidationResult.Succeeded;
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

            // We probably should only try to skip if operation is scan only, 
            // but currently that's the only implemented option.
            if (ShouldSkip(request))
            {
                return ValidationResult.Succeeded;
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

            // here we need to determine whether we do scan only or scan and repo sign.
            // Right now we only support scan only.

            await _scanAndSignEnqueuer.EnqueueScanAsync(request);
            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return result.ToValidationResult();
        }

        private bool ShouldSkip(IValidationRequest request)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(
                request.PackageId,
                request.PackageVersion);

            return ShouldSkip(request, package);
        }

        private bool ShouldSkip(IValidationRequest request, Package package)
        {
            if (!_criteriaEvaluator.IsMatch(_configuration.PackageCriteria, package))
            {
                _logger.LogInformation(
                    "The scan for {validationId} ({packageId} {packageVersion}) was skipped due to package criteria configuration.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return true;
            }

            return false;
        }
    }
}
