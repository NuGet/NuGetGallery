// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.ScanOnly)]
    public class ScanValidator : BaseValidator, IValidator
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICorePackageService _packageService;
        private readonly ICriteriaEvaluator<Package> _criteriaEvaluator;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly ScanAndSignConfiguration _configuration;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public ScanValidator(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            ICorePackageService packageService,
            ICriteriaEvaluator<Package> criteriaEvaluator,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor,
            ILogger<ScanAndSignProcessor> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _criteriaEvaluator = criteriaEvaluator ?? throw new ArgumentNullException(nameof(criteriaEvaluator));
            _scanAndSignEnqueuer = scanAndSignEnqueuer ?? throw new ArgumentNullException(nameof(scanAndSignEnqueuer));

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
                    "Scan only validation with validation Id {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return validatorStatus.ToValidationResult();
            }

            if (ShouldSkipScan(request))
            {
                return ValidationResult.Succeeded;
            }

            await _scanAndSignEnqueuer.EnqueueScanAsync(request.ValidationId, request.NupkgUrl);

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
    }
}
