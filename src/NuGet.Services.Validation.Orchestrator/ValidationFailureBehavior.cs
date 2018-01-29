// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Options for orchestrator for how to handle validation failure
    /// </summary>
    public enum ValidationFailureBehavior
    {
        /// <summary>
        /// Indicates that validation must succeed: if it fails, package would be marked as failed validation.
        /// </summary>
        MustSucceed,

        /// <summary>
        /// Indicates that the outcome of the validation does not affect package validation outcome: even if
        /// this validation fails, the package might still be marked as successully passed validation.
        /// </summary>
        AllowedToFail
    }
}
