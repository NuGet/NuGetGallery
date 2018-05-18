// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Contains useful information about what happened inside of <see cref="ValidationSetProcessor"/>
    /// during processing of the current message
    /// </summary>
    public class ValidationSetProcessorResult
    {
        /// <summary>
        /// true if there were any validation that succeeded (regardless of its settings)
        /// </summary>
        public bool AnyValidationSucceeded { get; set; } = false;

        /// <summary>
        /// true if any validation succeeded that had <see cref="ValidationConfigurationItem.FailureBehavior"/> 
        /// set to <see cref="ValidationFailureBehavior.MustSucceed"/>
        /// </summary>
        public bool AnyRequiredValidationSucceeded { get; set; } = false;
    }
}
