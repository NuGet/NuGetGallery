// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Interface for the code that deals with any changes that happened in the validation set
    /// </summary>
    public interface IValidationOutcomeProcessor
    {
        /// <summary>
        /// Processes the changes in validation statuses:
        /// * If there are failed vailidations, marks package as failed;
        /// * If all validations succeeded, copies package to public container and marks package as validated;
        /// * Otherwise, queues another check for the package
        /// </summary>
        /// <param name="validationSet">Current state of validation set</param>
        /// <param name="package">Package information from Gallery DB</param>
        /// <returns>A task that completes when the outcome has been processed</returns>
        Task ProcessValidationOutcomeAsync(PackageValidationSet validationSet, Package package);
    }
}
