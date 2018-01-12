// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Stores configuration of the single validation
    /// </summary>
    public class ValidationConfigurationItem
    {
        /// <summary>
        /// The name of the validation
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Timeout after which started validation is considered failed if it didn't produce any outcome
        /// </summary>
        public TimeSpan FailAfter { get; set; }

        /// <summary>
        /// List of validation names that must finish before this validation can run
        /// </summary>
        public List<string> RequiredValidations { get; set; }

        /// <summary>
        /// Specifies whether the validation should be started as part of the package validation process.
        /// </summary>
        public bool ShouldStart { get; set; }

        /// <summary>
        /// Specifies how orchestrator would handle the validation failure.
        /// See <see cref="ValidationFailureBehavior"/> for more details about specific values.
        /// </summary>
        public ValidationFailureBehavior FailureBehavior { get; set; }

        public ValidationConfigurationItem()
        {
            RequiredValidations = new List<string>();
        }
    }
}
