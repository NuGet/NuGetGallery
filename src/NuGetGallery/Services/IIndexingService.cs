// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery.Configuration;
using WebBackgrounder;
namespace NuGetGallery
{
    public interface IIndexingService
    {
        Task<DateTime?> GetLastWriteTime();
        Task UpdateIndex();
        Task UpdateIndex(bool forceRefresh);
        Task UpdatePackage(Package package);

        Task<int> GetDocumentCount();
        Task<long> GetIndexSizeInBytes();

        Task RegisterBackgroundJobs(IList<IJob> jobs);

        string IndexPath { get; }

        bool IsLocal { get; }
    }
}