// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class Auxiliary2AzureSearchConfiguration : AzureSearchJobConfiguration
    {
        public string DownloadsV1JsonUrl { get; set; }
        public TimeSpan MinPushPeriod { get; set; }
        public int MaxDownloadCountDecreases { get; set; } = 15000;
    }
}
