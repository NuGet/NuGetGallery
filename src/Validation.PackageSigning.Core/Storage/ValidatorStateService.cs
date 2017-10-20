// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class ValidatorStateService : IValidatorStateService
    {
        private const int UniqueConstraintViolationErrorCode = 2627;

        private IValidationEntitiesContext _validationContext;
        private string _validatorName;

        public ValidatorStateService(
            IValidationEntitiesContext validationContext,
            Type validatorType)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));

            if (validatorType == null)
            {
                throw new ArgumentNullException(nameof(validatorType));
            }

            if (!typeof(IValidator).IsAssignableFrom(validatorType))
            {
                throw new ArgumentException($"The validator type {validatorType} must extend {nameof(IValidator)}", nameof(validatorType));
            }

            _validatorName = validatorType.Name;
        }

        public async Task<ValidatorStatus> GetStatusAsync(IValidationRequest request)
        {
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
                    ValidatorName = _validatorName,
                    State = ValidationStatus.NotStarted,
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

        public Task<bool> IsRevalidationRequestAsync(IValidationRequest request)
        {
            return _validationContext
                        .ValidatorStatuses
                        .Where(s => s.PackageKey == request.PackageKey)
                        .Where(s => s.ValidatorName == _validatorName)
                        .Where(s => s.ValidationId != request.ValidationId)
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
            catch (DbUpdateException e) when (IsUniqueConstraintViolationException(e))
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
