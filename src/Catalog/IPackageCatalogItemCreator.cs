// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog
{
    public interface IPackageCatalogItemCreator
    {
        Task<PackageCatalogItem> CreateAsync(FeedPackageDetails packageItem, DateTime timestamp, CancellationToken cancellationToken);
    }
}