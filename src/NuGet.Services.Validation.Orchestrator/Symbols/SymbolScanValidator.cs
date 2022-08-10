// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGetGallery;

namespace NuGet.Services.Validation.Symbols
{
    [ValidatorName(ValidatorName.SymbolScan)]
    public class SymbolScanValidator : BaseNuGetValidator, INuGetValidator
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICoreSymbolPackageService _symbolPackageService;
        private readonly ICriteriaEvaluator<SymbolPackage> _criteriaEvaluator;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly SymbolScanOnlyConfiguration _configuration;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public SymbolScanValidator(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            ICoreSymbolPackageService packageService,
            ICriteriaEvaluator<SymbolPackage> criteriaEvaluator,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            IOptionsSnapshot<SymbolScanOnlyConfiguration> configurationAccessor,
            ILogger<ScanAndSignProcessor> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _symbolPackageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
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

        public async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            return validatorStatus.ToNuGetValidationResponse();
        }

        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
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

                return validatorStatus.ToNuGetValidationResponse();
            }

            if (ShouldSkipScan(request))
            {
                return NuGetValidationResponse.Succeeded;
            }

            await _scanAndSignEnqueuer.EnqueueScanAsync(request.ValidationId, request.NupkgUrl, context: new Dictionary<string, string>());

            var status = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return status.ToNuGetValidationResponse();
        }

        private bool ShouldSkipScan(INuGetValidationRequest request)
        {
            var symbolPackage = _symbolPackageService
                .FindSymbolPackagesByIdAndVersion(request.PackageId,request.PackageVersion)
                .FirstOrDefault(sp => sp.Key == request.PackageKey);

            if (symbolPackage == null)
            {
                throw new InvalidDataException($"The expected symbol package for {request.PackageId} {request.PackageVersion} not found!");
            }

            if (!_criteriaEvaluator.IsMatch(_configuration.PackageCriteria, symbolPackage))
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
