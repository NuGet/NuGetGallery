// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ImportAzureCdnStatistics
{
    internal interface ILeasedLogFile
        : IDisposable
    {
        string LeaseId { get; }

        string Uri { get; }

        CloudBlockBlob Blob { get; }

        Task AcquireInfiniteLeaseAsync();
    }
}