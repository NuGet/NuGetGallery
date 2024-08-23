// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NuGet.Protocol.Catalog;

namespace NuGet.Services
{
    public class InMemoryCatalogClient : ICatalogClient
    {
        public ConcurrentDictionary<string, PackageDetailsCatalogLeaf> PackageDetailsLeaves { get; } = new ConcurrentDictionary<string, PackageDetailsCatalogLeaf>();

        public Task<CatalogIndex> GetIndexAsync(string indexUrl)
        {
            throw new NotImplementedException();
        }

        public Task<CatalogLeaf> GetLeafAsync(string leafUrl)
        {
            throw new NotImplementedException();
        }

        public Task<PackageDeleteCatalogLeaf> GetPackageDeleteLeafAsync(string leafUrl)
        {
            throw new NotImplementedException();
        }

        public Task<PackageDetailsCatalogLeaf> GetPackageDetailsLeafAsync(string leafUrl)
        {
            return Task.FromResult(PackageDetailsLeaves[leafUrl]);
        }

        public Task<CatalogPage> GetPageAsync(string pageUrl)
        {
            throw new NotImplementedException();
        }
    }
}
