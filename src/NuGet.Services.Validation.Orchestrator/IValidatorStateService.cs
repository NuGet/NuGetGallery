// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The possible results for <see cref="IValidatorStateService.AddStatusAsync(string, ValidatorStatus)"/>.
    /// </summary>
    public enum AddStatusResult
    {
        /// <summary>
        /// Successfully persisted the <see cref="ValidatorStatus"/>.
        /// </summary>
        Success,

        /// <summary>
        /// Failed to persist the <see cref="ValidatorStatus"/> as a status already
        /// exists with the same validation id.
        /// </summary>
        StatusAlreadyExists,
    }

    /// <summary>
    /// The possible results for <see cref="IValidatorStateService.SaveStatusAsync(string, ValidatorStatus)"/>.
    /// </summary>
    public enum SaveStatusResult
    {
        /// <summary>
        /// Successfully persisted the updated <see cref="ValidatorStatus"/>
        /// </summary>
        Success,

        /// <summary>
        /// The <see cref="ValidatorStatus"/> is stale. The status should be refetched using
        /// <see cref="IValidatorStateService.GetStatusAsync(string, IValidationRequest)"/> before attempting
        /// to save again.
        /// </summary>
        StaleStatus,
    }

    /// <summary>
    /// A service used to persist a <see cref="IValidator"/>'s validation statuses.
    /// </summary>
    public interface IValidatorStateService
    {
        /// <summary>
        /// Get the persisted <see cref="ValidatorStatus"/> for the given <see cref="IValidationRequest"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="IValidator"/> whose status should be fetched.</typeparam>
        /// <param name="request">The request whose status should be fetched.</param>
        /// <returns>The persisted status of the validation request, or, a new ValidatorStatus if no status has been persisted.</returns>
        Task<ValidatorStatus> GetStatusAsync<T>(IValidationRequest request) where T : IValidator;

        /// <summary>
        /// Check if the request intends to revalidate a package that has already been validated by <see cref="TValidator"/> by
        /// a different validation request.
        /// </summary>
        /// <typeparam name="T">The <see cref="IValidator"/> whose statuses should be evaluated.</typeparam>
        /// <param name="request">The package validation request.</param>
        /// <returns>Whether the <see cref="TValidator"/> has already validated this request's package in a different validation request.</returns>
        Task<bool> IsRevalidationRequestAsync<T>(IValidationRequest request) where T : IValidator;

        /// <summary>
        /// Persist the status of a new validation request.
        /// </summary>
        /// <typeparam name="T">The <see cref="IValidator"/> whose status should be added.</typeparam>
        /// <param name="status">The status of the given validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task<AddStatusResult> AddStatusAsync<T>(ValidatorStatus status) where T : IValidator;

        /// <summary>
        /// Persist the status of an already existing validation request.
        /// </summary>
        /// <typeparam name="T">The <see cref="IValidator"/> whose status should be saved.</typeparam>
        /// <param name="status">The updated status for the validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task<SaveStatusResult> SaveStatusAsync<T>(ValidatorStatus status) where T : IValidator;
    }
}
