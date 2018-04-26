// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    [ValidatorName(ValidatorName.PackageSigning)]
    public class PackageSigningValidator : IProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IProcessSignatureEnqueuer _signatureVerificationEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageSigningValidator> _logger;

        public PackageSigningValidator(
            IValidatorStateService validatorStateService,
            IProcessSignatureEnqueuer signatureVerificationEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            ITelemetryService telemetryService,
            ILogger<PackageSigningValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            var validatorStatus = await _validatorStateService.GetStatusAsync(request);

            return validatorStatus.ToValidationResult();
        }

        public async Task<IValidationResult> StartAsync(IValidationRequest request)
        {
            var validatorStatus = await StartInternalAsync(request);

            return validatorStatus.ToValidationResult();
        }

        public async Task CleanUpAsync(IValidationRequest request)
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

        private async Task<ValidatorStatus> StartInternalAsync(IValidationRequest request)
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
            var stopwatch = Stopwatch.StartNew();

            await _signatureVerificationEnqueuer.EnqueueVerificationAsync(request);

            var result = await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);

            _telemetryService.TrackDurationToStartPackageSigningValidator(stopwatch.Elapsed);

            return result;
        }
    }
}
