// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Storage
{
    public class ValidatorStateService : IValidatorStateService
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ILogger<ValidatorStateService> _logger;
        private readonly string _validatorName;

        public ValidatorStateService(
            IValidationEntitiesContext validationContext,
            string validatorName,
            ILogger<ValidatorStateService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));
        }

        public async Task<ValidatorStatus> GetStatusAsync(INuGetValidationRequest request)
        {
            var status = await GetStatusAsync(request.ValidationId);

            if (status == null)
            {
                return new ValidatorStatus
                {
                    ValidationId = request.ValidationId,
                    PackageKey = request.PackageKey,
                    ValidatorName = _validatorName,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };
            }
            else if (status.PackageKey != request.PackageKey)
            {
                throw new ArgumentException(
                    $"Validation expected package key {status.PackageKey}, actual {request.PackageKey}",
                    nameof(request));
            }
            else if (status.ValidatorName != _validatorName)
            {
                throw new ArgumentException(
                    $"Validation expected validator {status.ValidatorName}, actual {_validatorName}",
                    nameof(request));
            }

            return status;
        }

        public Task<ValidatorStatus> GetStatusAsync(Guid validationId)
        {
            return _validationContext
                .ValidatorStatuses
                .Include(x => x.ValidatorIssues)
                .Where(s => s.ValidationId == validationId)
                .FirstOrDefaultAsync();
        }

        public Task<bool> IsRevalidationRequestAsync(INuGetValidationRequest request, ValidatingType validatingType)
        {
            return IsRevalidationRequestAsync(request.PackageKey, request.ValidationId, validatingType);
        }

        private Task<bool> IsRevalidationRequestAsync(int packageKey, Guid validationId, ValidatingType validatingType)
        {
            return _validationContext
                .ValidatorStatuses
                .Where(s => s.PackageKey == packageKey)
                .Where(s => s.ValidatorName == _validatorName)
                .Where(s => s.ValidatingType == validatingType)
                .Where(s => s.ValidationId != validationId)
                .AnyAsync();
        }

        public async Task<AddStatusResult> AddStatusAsync(ValidatorStatus status)
        {
            if (status.ValidatorName != _validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{_validatorName}', actual: '{status.ValidatorName}'",
                    nameof(status));
            }

            _validationContext.ValidatorStatuses.Add(status);

            try
            {
                await _validationContext.SaveChangesAsync();

                return AddStatusResult.Success;
            }
            catch (DbUpdateException e) when (e.IsUniqueConstraintViolationException())
            {
                return AddStatusResult.StatusAlreadyExists;
            }
        }

        public async Task<SaveStatusResult> SaveStatusAsync(ValidatorStatus status)
        {
            if (status.ValidatorName != _validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{_validatorName}', actual: '{status.ValidatorName}'",
                    nameof(status));
            }

            try
            {
                await _validationContext.SaveChangesAsync();

                return SaveStatusResult.Success;
            }
            catch (DbUpdateConcurrencyException)
            {
                return SaveStatusResult.StaleStatus;
            }
        }

        public async Task<ValidatorStatus> TryAddValidatorStatusAsync(INuGetValidationRequest request, ValidatorStatus status, ValidationStatus desiredState)
        {
            status.State = desiredState;

            var result = await AddStatusAsync(status);

            if (result == AddStatusResult.StatusAlreadyExists)
            {
                // The add operation fails if another instance of this service has already created the status.
                // This may happen due to repeated operations kicked off by the Orchestrator. Return the result from
                // the other add operation.
                _logger.LogWarning(
                    Error.ValidatorStateServiceFailedToAddStatus,
                    "Failed to add validation status for {ValidationId} ({PackageId} {PackageVersion}) as a record already exists",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return await GetStatusAsync(request);
            }
            else if (result != AddStatusResult.Success)
            {
                throw new NotSupportedException($"Unknown {nameof(AddStatusResult)}: {result}");
            }

            return status;
        }

        public async Task<ValidatorStatus> TryUpdateValidationStatusAsync(
            INuGetValidationRequest request,
            ValidatorStatus validatorStatus,
            ValidationStatus desiredState)
        {
            validatorStatus.State = desiredState;

            var result = await SaveStatusAsync(validatorStatus);

            if (result == SaveStatusResult.StaleStatus)
            {
                // The save operation fails if another instance of this service has already modified the status.
                // This may happen due to repeated operations kicked off by the Orchestrator. Return the result
                // from the other update.
                _logger.LogWarning(
                    Error.ValidatorStateServiceFailedToUpdateStatus,
                    "Failed to save validation status for {ValidationId} ({PackageId} {PackageVersion}) as the current status is stale",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return await GetStatusAsync(request);
            }
            else if (result != SaveStatusResult.Success)
            {
                throw new NotSupportedException($"Unknown {nameof(SaveStatusResult)}: {result}");
            }

            return validatorStatus;
        }
    }
}
