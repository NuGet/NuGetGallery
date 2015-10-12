// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests
{
    public static class Catalogs
    {
        public static MemoryStorage CreateTestCatalogWithThreePackages()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.Add(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesIndex));

            catalogStorage.Content.Add(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesPage));

            catalogStorage.Content.Add(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/listedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage100));

            catalogStorage.Content.Add(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesUnlistedPackage100));

            catalogStorage.Content.Add(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/listedpackage.1.0.1.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage101));
            
            return catalogStorage;
        }
    }
}