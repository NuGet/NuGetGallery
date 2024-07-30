// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Interface for the code that deals with any changes that happened in the validation set
    /// </summary>
    public interface IValidationOutcomeProcessor<T> where T : class, IEntity
    {
        /// <summary>
        /// Processes the changes in validation statuses:
        /// * If there are failed vailidations, marks package as failed;
        /// * If all validations succeeded, copies package to public container and marks package as validated;
        /// * Otherwise, queues another check for the package
        /// </summary>
        /// <param name="validationSet">Current state of validation set</param>
        /// <param name="validatingEntity">The validating entity.</param>
        /// <param name="currentCallStats">Contains information about what happened during current message processing in
        /// the validation set processor.</param>
        /// <param name="scheduleNextCheck">Whether or not the next check should be scheduled.</param>
        /// <returns>A task that completes when the outcome has been processed</returns>
        Task ProcessValidationOutcomeAsync(
            PackageValidationSet validationSet,
            IValidatingEntity<T> validatingEntity,
            ValidationSetProcessorResult currentCallStats,
            bool scheduleNextCheck);
    }
}
