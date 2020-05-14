// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    public interface IFileDownloader
    {
        Task<FileDownloadResult> DownloadAsync(Uri fileUri, CancellationToken cancellationToken);
        Task<FileDownloadResult> DownloadExpectedFileSizeAsync(Uri fileUri, long maxFileSize, CancellationToken cancellationToken);
    }
}