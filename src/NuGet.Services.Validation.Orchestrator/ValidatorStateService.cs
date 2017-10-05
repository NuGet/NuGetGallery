// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorStateService : IValidatorStateService
    {
        private const int UniqueConstraintViolationErrorCode = 2627;

        private IValidationEntitiesContext _validationContext;

        public ValidatorStateService(IValidationEntitiesContext validationContext)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
        }

        public async Task<ValidatorStatus> GetStatusAsync<T>(IValidationRequest request)
            where T : IValidator
        {
            var validatorName = typeof(T).Name;
            var status = await _validationContext
                                    .ValidatorStatuses
                                    .Where(s => s.ValidationId == request.ValidationId)
                                    .FirstOrDefaultAsync();

            if (status == null)
            {
                return new ValidatorStatus
                {
                    ValidationId = request.ValidationId,
                    PackageKey = request.PackageKey,
                    ValidatorName = validatorName,
                    State = ValidationStatus.NotStarted,
                };
            }
            else if (status.PackageKey != request.PackageKey)
            {
                throw new ArgumentException(
                    $"Validation expected package key {status.PackageKey}, actual {request.PackageKey}",
                    nameof(request));
            }
            else if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Validation expected validator {status.ValidatorName}, actual {validatorName}",
                    nameof(request));
            }

            return status;
        }

        public Task<bool> IsRevalidationRequestAsync<T>(IValidationRequest request)
            where T : IValidator
        {
            var validatorName = typeof(T).Name;

            return _validationContext
                        .ValidatorStatuses
                        .Where(s => s.PackageKey == request.PackageKey)
                        .Where(s => s.ValidatorName == validatorName)
                        .Where(s => s.ValidationId != request.ValidationId)
                        .AnyAsync();
        }

        public async Task<AddStatusResult> AddStatusAsync<T>(ValidatorStatus status)
            where T : IValidator
        {
            var validatorName = typeof(T).Name;

            if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{validatorName}', actual: '{status.ValidatorName}'",
                    nameof(status));
            }

            _validationContext.ValidatorStatuses.Add(status);

            try
            {
                await _validationContext.SaveChangesAsync();

                return AddStatusResult.Success;
            }
            catch (DbUpdateException e) when (IsUniqueConstraintViolationException(e))
            {
                return AddStatusResult.StatusAlreadyExists;
            }
        }

        public async Task<SaveStatusResult> SaveStatusAsync<T>(ValidatorStatus status) where T : IValidator
        {
            var validatorName = typeof(T).Name;

            if (status.ValidatorName != validatorName)
            {
                throw new ArgumentException(
                    $"Expected validator name '{validatorName}', actual: '{status.ValidatorName}'",
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

        private static bool IsUniqueConstraintViolationException(DbUpdateException e)
        {
            var sqlException = e.GetBaseException() as SqlException;

            if (sqlException != null)
            {
                return sqlException.Errors.Cast<SqlError>().Any(error => error.Number == UniqueConstraintViolationErrorCode);
            }

            return false;
        }
    }
}
