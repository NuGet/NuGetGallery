// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Stores the orchestrator configuration
    /// </summary>
    public class ValidationConfiguration
    {
        /// <summary>
        /// List of validations to run with their settings and dependencies
        /// </summary>
        public List<ValidationConfigurationItem> Validations { get; set; }
    }
}
