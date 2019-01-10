// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchServiceConfiguration : AzureSearchConfiguration
    {
        public string SemVer1RegistrationsBaseUrl { get; set; }
        public string SemVer2RegistrationsBaseUrl { get; set; }
        public string AuxiliaryDataStorageConnectionString { get; set; }
        public string AuxiliaryDataStorageContainer { get; set; }
        public string AuxiliaryDataStorageDownloadsPath { get; set; }
        public string AuxiliaryDataStorageVerifiedPackagesPath { get; set; }
        public TimeSpan AuxiliaryDataReloadFrequency { get; set; }
        public TimeSpan AuxiliaryDataReloadFailureRetryFrequency { get; set; }
        public string DeploymentLabel { get; set; }
    }
}
