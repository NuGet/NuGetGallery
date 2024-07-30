// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchJobDevelopmentConfiguration
    {
        /// <summary>
        /// Disabling version lists writers speeds up db2azuresearch but breaks catalog2azuresearch.
        /// This should be false on production environments.
        /// </summary>
        public bool DisableVersionListWriters { get; set; }
    }
}
