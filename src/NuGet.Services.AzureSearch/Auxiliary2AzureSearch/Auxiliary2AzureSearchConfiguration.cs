// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class Auxiliary2AzureSearchConfiguration : AzureSearchJobConfiguration, IAuxiliaryDataStorageConfiguration
    {
        public string AuxiliaryDataStorageConnectionString { get; set; }
        public string AuxiliaryDataStorageContainer { get; set; }
        public string AuxiliaryDataStorageDownloadsPath { get; set; }
        public string AuxiliaryDataStorageVerifiedPackagesPath { get; set; }
        public string AuxiliaryDataStorageExcludedPackagesPath { get; }
        public TimeSpan MinPushPeriod { get; set; }
    }
}
