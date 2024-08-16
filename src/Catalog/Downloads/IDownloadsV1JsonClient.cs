// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public delegate void AddDownloadCount(string packageId, string packageVersion, long downloadCount);

    public interface IDownloadsV1JsonClient
    {
        Task<DownloadData> ReadAsync(string url);
        Task ReadAsync(string url, AddDownloadCount addCount);
    }
}