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
        string AuxiliaryDataStorageVerifiedPackagesPath { get; }
    }
}
