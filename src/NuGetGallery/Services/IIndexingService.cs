// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using WebBackgrounder;

namespace NuGetGallery
{
    public interface IIndexingService
    {
        Task<DateTime?> GetLastWriteTime();
        void UpdateIndex();
        void UpdateIndex(bool forceRefresh);
        void UpdatePackage(Package package);
        void UpdatePackageRegistration(PackageRegistration packageRegistration);

        Task<int> GetDocumentCount();
        Task<long> GetIndexSizeInBytes();

        void RegisterBackgroundJobs(IList<IJob> jobs, IAppConfiguration configuration);

        string IndexPath { get; }

        bool IsLocal { get; }
    }
}