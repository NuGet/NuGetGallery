// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.ScanAndSign)]
    public class ScanAndSignProcessor : IProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IScanAndSignEnqueuer _scanAndSignEnqueuer;
        private readonly ILogger<ScanAndSignProcessor> _logger;

        public ScanAndSignProcessor(
            IValidatorStateService validatorStateService,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            ILogger<ScanAndSignProcessor> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _scanAndSignEnqueuer = scanAndSignEnqueuer ?? throw new ArgumentNullException(nameof(scanAndSignEnqueuer));
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

            // here we need to determine whether we do scan only or scan and repo sign.
            // Right now we only support scan only

            await _scanAndSignEnqueuer.EnqueueScanAsync(request);
            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return result.ToValidationResult();
        }
    }
}
