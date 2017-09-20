// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Stores configuration for the "Validate only" run mode
    /// </summary>
    public class ValidateOnlyConfiguration
    {
        /// <summary>
        /// Indicates whether the orchestrator should only check the configuration (true) or run the service (false)
        /// </summary>
        public bool ValidateOnly { get; set; }
    }
}
