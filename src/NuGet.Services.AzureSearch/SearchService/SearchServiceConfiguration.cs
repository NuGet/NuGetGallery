// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchServiceConfiguration : AzureSearchConfiguration
    {
        public float NamespaceBoost { get; set; } = 100;
        public float SeparatorSplitBoost { get; set; } = 2;
        public float ExactMatchBoost { get; set; } = 1000;
        public string SemVer1RegistrationsBaseUrl { get; set; }
        public string SemVer2RegistrationsBaseUrl { get; set; }
        public TimeSpan AuxiliaryDataReloadFrequency { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan AuxiliaryDataReloadFailureRetryFrequency { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan SecretRefreshFrequency { get; set; } = TimeSpan.FromHours(12);
        public TimeSpan SecretRefreshFailureRetryFrequency { get; set; } = TimeSpan.FromMinutes(5);
        public string DeploymentLabel { get; set; }
        public List<string> TestOwners { get; set; } = new List<string>();
        public int V2DeepPagingLimit { get; set; } = 30_000;
        public int V3DeepPagingLimit { get; set; } = 3_000;
    }
}
