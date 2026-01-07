// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Storage
{
    /// <summary>
    /// A service used to persist a <see cref="INuGetValidator"/>'s validation statuses.
    /// </summary>
    public interface IValidatorStateService
    {
        /// <summary>
        /// Get the persisted <see cref="ValidatorStatus"/> for the given <see cref="INuGetValidationRequest"/>.
        /// </summary>
        /// <param name="request">The request whose status should be fetched.</param>
        /// <returns>The persisted status of the validation request, or, a new ValidatorStatus if no status has been persisted.</returns>
        Task<ValidatorStatus> GetStatusAsync(INuGetValidationRequest request);

        /// <summary>
        /// Get the persisted <see cref="ValidatorStatus"/> for the given validation ID.
        /// </summary>
        /// <param name="validationId">The validation ID of the validator status to be fetched.</param>
        /// <returns>The persisted status of the validation request, or null if no status has been persisted.</returns>
        Task<ValidatorStatus> GetStatusAsync(Guid validationId);

        /// <summary>
        /// Get the persisted <see cref="ValidatorStatus"/> for the given <see cref="IValidationRequest"/>.
        /// </summary>
        /// <param name="request">The request whose status should be fetched.</param>
        /// <returns>The persisted status of the validation request, or, a new ValidatorStatus if no status has been persisted.</returns>
        Task<ValidatorStatus> GetStatusAsync(IValidationRequest request);

        /// <summary>
        /// Check if the request intends to revalidate a package that has already been validated by <see cref="TValidator"/> by
        /// a different validation request.
        /// </summary>
        /// <param name="request">The package validation request.</param>
        /// <param name="validatingType">The <see cref="ValidatingType"/>.</param>
        /// <returns>Whether the <see cref="TValidator"/> has already validated this request's package in a different validation request.</returns>
        Task<bool> IsRevalidationRequestAsync(INuGetValidationRequest request, ValidatingType validatingType);

        /// <summary>
        /// Persist the status of a new validation request.
        /// </summary>
        /// <param name="status">The status of the given validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task<AddStatusResult> AddStatusAsync(ValidatorStatus status);

        /// <summary>
        /// Persist the status of an already existing validation request.
        /// </summary>
        /// <param name="status">The updated status for the validation request.</param>
        /// <returns>The persisted state. This may not be the desired state if the save operation fails.</returns>
        Task<SaveStatusResult> SaveStatusAsync(ValidatorStatus status);

        /// <summary>
        /// Try to persist a new validation request's validator status. If the add operation fails, the result of
        /// <see cref="GetStatusAsync(INuGetValidationRequest)"/> will be returned instead.
        /// </summary>
        /// <param name="request">The request to validate a package whose state should be updated.</param>
        /// <param name="validatorStatus">The validaiton request's validator status that should be added to the database.</param>
        /// <param name="desiredState">The desired state for the validator's status.</param>
        /// <returns>The persisted state. This may not be the desired state if the add operation fails.</returns>
        Task<ValidatorStatus> TryAddValidatorStatusAsync(INuGetValidationRequest request, ValidatorStatus status, ValidationStatus desiredState);

        /// <summary>
        /// Try to persist a new validation request's validator status. If the add operation fails, the result of
        /// <see cref="GetStatusAsync(IValidationRequest)"/> will be returned instead.
        /// </summary>
        /// <param name="request">The request to validate a package whose state should be updated.</param>
        /// <param name="validatorStatus">The validaiton request's validator status that should be added to the database.</param>
        /// <param name="desiredState">The desired state for the validator's status.</param>
        /// <returns>The persisted state. This may not be the desired state if the add operation fails.</returns>
        Task<ValidatorStatus> TryAddValidatorStatusAsync(IValidationRequest request, ValidatorStatus status, ValidationStatus desiredState);

        /// <summary>
        /// Try to persist the validator's status. If the update fails, the result of <see cref="GetStatusAsync(INuGetValidationRequest)"/>
        /// will be returned instead.
        /// </summary>
        /// <param name="request">The request to validate a package whose state should be updated.</param>
        /// <param name="validatorStatus">The state of the validator's validation request that should be updated.</param>
        /// <param name="desiredState">The desired state for the validator's status.</param>
        /// <returns>The persisted state. This may not be the desired state if the update operation fails.</returns>
        Task<ValidatorStatus> TryUpdateValidationStatusAsync(INuGetValidationRequest request, ValidatorStatus validatorStatus, ValidationStatus desiredState);

        /// <summary>
        /// Try to persist the validator's status. If the update fails, the result of <see cref="GetStatusAsync(IValidationRequest)"/>
        /// will be returned instead.
        /// </summary>
        /// <param name="request">The request to validate a package whose state should be updated.</param>
        /// <param name="validatorStatus">The state of the validator's validation request that should be updated.</param>
        /// <param name="desiredState">The desired state for the validator's status.</param>
        /// <returns>The persisted state. This may not be the desired state if the update operation fails.</returns>
        Task<ValidatorStatus> TryUpdateValidationStatusAsync(IValidationRequest request, ValidatorStatus validatorStatus, ValidationStatus desiredState);

    }
}
