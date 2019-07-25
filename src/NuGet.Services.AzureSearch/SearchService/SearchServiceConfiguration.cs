// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchServiceConfiguration : AzureSearchConfiguration, IAuxiliaryDataStorageConfiguration
    {
        public float MatchAllTermsBoost { get; set; } = 3;
        public string SemVer1RegistrationsBaseUrl { get; set; }
        public string SemVer2RegistrationsBaseUrl { get; set; }
        public string AuxiliaryDataStorageConnectionString { get; set; }
        public string AuxiliaryDataStorageContainer { get; set; }
        public string AuxiliaryDataStorageDownloadsPath { get; set; }
        public string AuxiliaryDataStorageVerifiedPackagesPath { get; set; }
        public string AuxiliaryDataStorageExcludedPackagesPath { get; set; }
        public TimeSpan AuxiliaryDataReloadFrequency { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan AuxiliaryDataReloadFailureRetryFrequency { get; set; } = TimeSpan.FromSeconds(30);
        public string DeploymentLabel { get; set; }
    }
}
