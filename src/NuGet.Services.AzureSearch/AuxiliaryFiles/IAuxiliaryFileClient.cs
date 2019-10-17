// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IAuxiliaryFileClient
    {
        Task<DownloadData> LoadDownloadDataAsync();
        Task<IReadOnlyDictionary<string, long>> LoadDownloadOverridesAsync();
        Task<HashSet<string>> LoadVerifiedPackagesAsync();
        Task<HashSet<string>> LoadExcludedPackagesAsync();
    }
}