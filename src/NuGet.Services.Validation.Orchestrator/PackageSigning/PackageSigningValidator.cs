// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSigningValidator : BaseValidator, IValidator
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IPackageSignatureVerificationEnqueuer _signatureVerificationEnqueuer;
        private readonly ILogger<PackageSigningValidator> _logger;

        public PackageSigningValidator(
            IValidatorStateService validatorStateService,
            IPackageSignatureVerificationEnqueuer signatureVerificationEnqueuer,
            ILogger<PackageSigningValidator> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
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
            await _signatureVerificationEnqueuer.EnqueueVerificationAsync(request);

            return await _validatorStateService.TryAddValidatorStatusAsync(request, validatorStatus, ValidationStatus.Incomplete);
        }
    }
}
