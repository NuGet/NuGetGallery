// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace UpdateBlobProperties
{
    public abstract class BlobInfo
    {
        public abstract string GetContainerName();

        public abstract string GetBlobName(PackageInfo packageInfo);

        public abstract IDictionary<string, string> GetUpdatedBlobProperties();

        public abstract Func<IEntityRepository<Package>, int, int, int, Task<List<PackageInfo>>> GetPageOfPackageInfosToUpdateBlobsAsync
        {
            get;
        }
    }
}
