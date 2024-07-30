// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Symbols
{
    [ValidatorName(ValidatorName.SymbolsValidator)]
    public class SymbolsValidator : BaseNuGetValidator, INuGetValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly ISymbolsMessageEnqueuer _symbolMessageEnqueuer;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolsValidator> _logger;

        public SymbolsValidator(
            IValidatorStateService validatorStateService,
            ISymbolsMessageEnqueuer symbolMessageEnqueuer,
            ITelemetryService telemetryService,
            ILogger<SymbolsValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _symbolMessageEnqueuer = symbolMessageEnqueuer ?? throw new ArgumentNullException(nameof(symbolMessageEnqueuer));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);
            var response = validatorStatus.ToNuGetValidationResponse();
            if (validatorStatus.State == ValidationStatus.Failed)
            {
                _logger.LogInformation(
                    "SymbolValidationFailure "+
                    "status = {ValidationStatus}, snupkg URL = {NupkgUrl}, validation issues = {Issues}",
                    response.Status,
                    response.NupkgUrl,
                    response.Issues.Select(i => i.IssueCode));
            }
            return response;
        }

        /// <summary>
        /// The pattern used for the StartAsync:
        /// 1. Check if a validation was already started
        /// 2. Only if a validation was not started queue the message to be processed.
        /// 3. After the message is queued, update the ValidatorStatus for the <paramref name="request"/>.
        /// </summary>
        /// <param name="request">The request to be send to the validator job queue.</param>
        /// <returns>The validation status.</returns>
        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var validatorStatus = await _validatorStateService.GetStatusAsync(request);
            // See issue https://github.com/NuGet/NuGetGallery/issues/6249
            validatorStatus.ValidatingType = ValidatingType.SymbolPackage;

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Symbol validation for {PackageId} {PackageNormalizedVersion} has already started.",
                    request.PackageId,
                    request.PackageVersion);

                return validatorStatus.ToNuGetValidationResponse();
            }

            // Due to race conditions or failure of method TryAddValidatorStatusAsync the same message can be enqueued multiple times
            // Log this information to postmortem evaluate this behavior
            _telemetryService.TrackSymbolsMessageEnqueued(request.PackageId, request.PackageVersion, ValidatorName.SymbolsValidator, request.ValidationId);
            await _symbolMessageEnqueuer.EnqueueSymbolsValidationMessageAsync(request);

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            return result.ToNuGetValidationResponse();
        }
    }
}
