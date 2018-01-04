// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Abstracts the validation persistence layer
    /// </summary>
    public interface IValidationStorageService
    {
        /// <summary>
        /// Persists the passed validation set in the storage
        /// </summary>
        /// <param name="packageValidationSet"><see cref="PackageValidationSet"/> instance to persist.</param>
        /// <returns>Persisted object with any default values set by storage and tracked if storage provides change tracking.</returns>
        Task<PackageValidationSet> CreateValidationSetAsync(PackageValidationSet packageValidationSet);

        /// <summary>
        /// Tries to read <see cref="PackageValidationSet"/> from storage by <paramref name="validationTrackingId"/>.
        /// </summary>
        /// <param name="validationTrackingId">Tracking id of the validation set to read.</param>
        /// <returns>Validation set instance if found, null otherwise.</returns>
        Task<PackageValidationSet> GetValidationSetAsync(Guid validationTrackingId);

        /// <summary>
        /// Updates the passed <see cref="PackageValidation"/> with the validation result's status,
        /// updates the <see cref="PackageValidation.ValidationStatusTimestamp"/> to current timestamp,
        /// and persists changes in the storage. The result's status cannot be <see cref="ValidationStatus.NotStarted"/>
        /// </summary>
        /// <param name="packageValidation">Validation object to update, must be an object from the <see cref="PackageValidationSet.PackageValidations"/> collection
        /// from an <see cref="PackageValidationSet"/> previously returned by either <see cref="CreateValidationSetAsync(PackageValidationSet)"/> 
        /// or <see cref="GetValidationSetAsync(Guid)"/> calls.</param>
        /// <param name="validationResult">Validation result. Its status cannot be <see cref="ValidationStatus.NotStarted"/></param>
        /// <returns>Task object tracking the async operation status.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="validationResult"/> has status <see cref="ValidationStatus.NotStarted"/></exception>
        Task MarkValidationStartedAsync(PackageValidation packageValidation, IValidationResult validationResult);

        /// <summary>
        /// Updates the passed <see cref="PackageValidation"/> object with the result's validation status,
        /// updates the <see cref="PackageValidation.ValidationStatusTimestamp"/> property to the current
        /// timestamp, adds the result's <see cref="PackageValidationIssue"/>s to the validation, and then persists
        /// changes in the storage.
        /// </summary>
        /// <param name="packageValidation">Validation object to update, must be an object from the <see cref="PackageValidationSet.PackageValidations"/> collection
        /// from an <see cref="PackageValidationSet"/> previously returned by either <see cref="CreateValidationSetAsync(PackageValidationSet)"/> 
        /// or <see cref="GetValidationSetAsync(Guid)"/> calls.</param>
        /// <param name="validationResult">The result of the validation.</param>
        /// <returns>Task object tracking the async operation status.</returns>
        Task UpdateValidationStatusAsync(PackageValidation packageValidation, IValidationResult validationResult);
    }
}