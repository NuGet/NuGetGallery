// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public interface IGalleryDatabaseQueryService
    {
        Task<FeedPackageDetails> GetPackageOrNull(string id, string version);
        Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesCreatedSince(DateTime since, int top);
        Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesEditedSince(DateTime since, int top);
    }
}