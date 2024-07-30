// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Stats.ImportAzureCdnStatistics
{
    public interface IStatisticsBlobContainerUtility
    {
        Task<Stream> OpenCompressedBlobAsync(ILeasedLogFile logFile);
        Task CopyToDeadLetterContainerAsync(ILeasedLogFile logFile, Exception e);
        Task DeleteSourceBlobAsync(ILeasedLogFile logFile);
        Task ArchiveBlobAsync(ILeasedLogFile logFile);
    }
}