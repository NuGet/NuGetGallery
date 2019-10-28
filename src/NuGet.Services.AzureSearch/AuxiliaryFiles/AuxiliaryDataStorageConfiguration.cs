// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryDataStorageConfiguration : IAuxiliaryDataStorageConfiguration
    {
        public string AuxiliaryDataStorageConnectionString { get; set; }
        public string AuxiliaryDataStorageContainer { get; set; }
        public string AuxiliaryDataStorageDownloadsPath { get; set; }
        public string AuxiliaryDataStorageDownloadOverridesPath { get; set; }
        public string AuxiliaryDataStorageExcludedPackagesPath { get; set; }
        public string AuxiliaryDataStorageVerifiedPackagesPath { get; set; }
    }
}
