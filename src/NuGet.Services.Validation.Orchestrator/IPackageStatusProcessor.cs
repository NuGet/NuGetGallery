// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// This interface manages the state of gallery artifacts: gallery DB and packages container.
    /// </summary>
    public interface IStatusProcessor<T> where T : class, IEntity
    {
        Task SetStatusAsync(
            IValidatingEntity<T> validatingEntity,
            PackageValidationSet validationSet,
            PackageStatus status);

        /// <summary>
        /// Applies a terminal validation status to a staged entity without publishing it.
        /// </summary>
        /// <param name="validatingEntity">The staged entity being validated.</param>
        /// <param name="validationSet">The validation set producing the outcome.</param>
        /// <param name="status">The terminal staging status to apply.</param>
        /// <returns>A task that completes when the staging status has been processed.</returns>
        Task SetStagedValidationStatusAsync(
            IValidatingEntity<T> validatingEntity,
            PackageValidationSet validationSet,
            StagedPackageStatus status);
    }
}