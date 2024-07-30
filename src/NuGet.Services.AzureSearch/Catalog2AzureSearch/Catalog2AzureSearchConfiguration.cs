// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.V3;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class Catalog2AzureSearchConfiguration : AzureSearchJobConfiguration, ICommitCollectorConfiguration
    {
        public int MaxConcurrentCatalogLeafDownloads { get; set; } = 64;
        public bool CreateContainersAndIndexes { get; set; }
        public string Source { get; set; }
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public List<string> DependencyCursorUrls { get; set; }
        public string RegistrationsBaseUrl { get; set; }
    }
}
