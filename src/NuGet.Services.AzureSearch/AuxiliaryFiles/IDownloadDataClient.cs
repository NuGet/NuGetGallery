﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IDownloadDataClient
    {
        Task<AuxiliaryFileResult<DownloadData>> ReadLatestIndexedAsync(IAccessCondition accessCondition, StringCache stringCache);
        Task ReplaceLatestIndexedAsync(DownloadData newData, IAccessCondition accessCondition);
    }
}