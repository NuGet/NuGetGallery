// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IAuxiliaryDataStorageConfiguration
    {
        string AuxiliaryDataStorageConnectionString { get; }
        string AuxiliaryDataStorageContainer { get; }
        string AuxiliaryDataStorageDownloadsPath { get; }
        string AuxiliaryDataStorageExcludedPackagesPath { get; }

        /// <summary>
        /// The URL to get downloads.v1.json. This property, if set, takes precedence over <see cref="AuxiliaryDataStorageDownloadsPath"/>.
        /// This setting allows the downloads.v1.json report to be generated in a place different from
        /// <see cref="AuxiliaryDataStorageExcludedPackagesPath"/>.
        /// </summary>
        string DownloadsV1JsonUrl { get; }
    }
}
