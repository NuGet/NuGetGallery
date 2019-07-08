// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Indexing;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IAuxiliaryFileClient
    {
        Task<DownloadData> LoadDownloadDataAsync();
        Task<AuxiliaryFileResult<Downloads>> LoadDownloadsAsync(string etag);
        Task<AuxiliaryFileResult<HashSet<string>>> LoadVerifiedPackagesAsync(string etag);
    }
}