// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.V3;

namespace NuGet.Jobs.RegistrationComparer
{
    public class RegistrationComparerConfiguration : ICommitCollectorConfiguration
    {
        public string StorageConnectionString { get; set; }
        public string StorageContainer { get; set; }
        public List<HivesConfiguration> Registrations { get; set; }
        public string Source { get; set; }
        public int MaxConcurrentCatalogLeafDownloads { get; set; } = 64;
        public int MaxConcurrentComparisons { get; set; } = 32;
        public int MaxConcurrentPageAndLeafDownloadsPerId { get; set; } = 32;
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}
