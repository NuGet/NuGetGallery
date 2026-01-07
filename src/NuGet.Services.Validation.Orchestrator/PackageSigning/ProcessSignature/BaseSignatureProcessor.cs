// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    public abstract class BaseSignatureProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IProcessSignatureEnqueuer _signatureVerificationEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<BaseSignatureProcessor> _logger;

        public BaseSignatureProcessor(
            IValidatorStateService validatorStateService,
            IProcessSignatureEnqueuer signatureVerificationEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            ITelemetryService telemetryService,
            ILogger<BaseSignatureProcessor> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            return validatorStatus.ToNuGetValidationResponse();
        }

        public virtual async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            var validatorStatus = await StartInternalAsync(request);

            return validatorStatus.ToNuGetValidationResponse();
        }

        public async Task CleanUpAsync(INuGetValidationRequest request)
        {
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

        /// <summary>
        /// Whether the package MUST have an acceptable repository signature to pass validation.
        /// </summary>
        protected abstract bool RequiresRepositorySignature { get; }

        private async Task<ValidatorStatus> StartInternalAsync(INuGetValidationRequest request)
        {
            // Check that this is the first validation for this specific request.
            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            if (validatorStatus.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Package Signing validation with validationId {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return validatorStatus;
            }

            // Kick off the verification process. Note that the jobs will not verify the package until the
            // state of this validator has been persisted to the database.
            using (_telemetryService.TrackDurationToStartPackageSigningValidator(request.PackageId, request.PackageVersion))
            {
                await _signatureVerificationEnqueuer.EnqueueProcessSignatureAsync(request, RequiresRepositorySignature);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);
            }
        }
    }
}
