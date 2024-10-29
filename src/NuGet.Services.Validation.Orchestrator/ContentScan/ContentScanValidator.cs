// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.ContentScan;
using NuGet.Jobs.Validation.Storage;

namespace NuGet.Services.Validation.Orchestrator.ContentScan
{
    [ValidatorName(ValidatorName.ContentScanValidator)]
    public class ContentScanValidator : IValidator
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly IContentScanEnqueuer _contentScanEnqueuer;
        private readonly ContentScanConfiguration _configuration;
        private readonly ILogger<ContentScanValidator> _logger;

        public ContentScanValidator(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            IContentScanEnqueuer contentScanEnqueuer,
            IOptionsSnapshot<ContentScanConfiguration> configurationAccessor,
            ILogger<ContentScanValidator> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _contentScanEnqueuer = contentScanEnqueuer ?? throw new ArgumentNullException(nameof(contentScanEnqueuer));

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

        public async Task<IValidationResponse> StartAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await GetValidatorStatusAsync(request);

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Content scan validation with validation Id {ValidationStepId} has already started.",
                    request.ValidationStepId);

                return GetValidationResponse(validatorStatus);
            }

            await _contentScanEnqueuer.EnqueueContentScanAsync(request.ValidationStepId, request.InputUrl);

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return GetValidationResponse(result);
        }

        public Task CleanUpAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            return Task.CompletedTask;
        }

        public async Task<IValidationResponse> GetResponseAsync(IValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = await GetValidatorStatusAsync(request);

            return GetValidationResponse(result);
        }

        private async Task<ValidatorStatus> GetValidatorStatusAsync(IValidationRequest request)
        {
            return await _validatorStateService.GetStatusAsync(request);
        }

        private IValidationResponse GetValidationResponse(ValidatorStatus status)
        {
            if (status.State == ValidationStatus.Failed)
            {
                var results = new List<PackageValidationResult>
                {
                    new PackageValidationResult
                    {
                        Type = "ContentViolation",
                        Data = "{}",
                    }
                };

                return new ValidationResponse(status.State, results);
            }

            return new ValidationResponse(status.State);
        }

    }
}
