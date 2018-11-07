// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Interface for the implementation that resolves the validation dependencies and runs them in proper order
    /// </summary>
    public interface IValidationSetProcessor
    {
        /// <summary>
        /// Checks the status of all incomplete validations, starts the ones that can be started (due to dependency changes).
        /// Persists all validation status changes.
        /// </summary>
        /// <param name="validationSet">Validation set to work with. Any validation updates would be reflected in that object upon return.</param>
        /// <returns>Information about what happened during processing of the message.</returns>
        Task<ValidationSetProcessorResult> ProcessValidationsAsync(PackageValidationSet validationSet);
    }
}
