// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchServiceConfiguration : AzureSearchConfiguration
    {
        public bool AllIconsInFlatContainer { get; set; }
        public float MatchAllTermsBoost { get; set; } = 3;
        public float ExactMatchBoost { get; set; } = 1000;
        public string SemVer1RegistrationsBaseUrl { get; set; }
        public string SemVer2RegistrationsBaseUrl { get; set; }
        public TimeSpan AuxiliaryDataReloadFrequency { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan AuxiliaryDataReloadFailureRetryFrequency { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan SecretRefreshFrequency { get; set; } = TimeSpan.FromHours(12);
        public TimeSpan SecretRefreshFailureRetryFrequency { get; set; } = TimeSpan.FromMinutes(5);
        public string DeploymentLabel { get; set; }
    }
}
