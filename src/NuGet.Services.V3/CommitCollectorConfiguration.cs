// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.V3
{
    public class CommitCollectorConfiguration : ICommitCollectorConfiguration
    {
        public int MaxConcurrentCatalogLeafDownloads { get; set; } = 64;
        public string Source { get; set; }
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(1);
    }
}
