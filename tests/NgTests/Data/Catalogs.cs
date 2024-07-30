// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Data
{
    public static class Catalogs
    {
        public static MemoryStorage CreateTestCatalogWithThreePackages()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesPage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/listedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesUnlistedPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/listedpackage.1.0.1.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage101));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithCommitThenTwoPackageCommit()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithCommitThenTwoPackageCommitIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithCommitThenTwoPackageCommitPage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesUnlistedPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/listedpackage.1.0.1.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage101));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/anotherpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithCommitThenTwoPackageCommitAnotherPackage100));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithThreePackagesAndDelete()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesAndDeleteIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesAndDeletePage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/listedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesUnlistedPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.55/listedpackage.1.0.1.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage101));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.13.06.40.07/otherpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesOtherPackage100));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithPackageCreatedThenDeleted()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithPackageCreatedThenDeletedIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithPackageCreatedThenDeletedPage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/otherpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithPackageCreatedThenDeletedPageOtherPackage100Created));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.13.06.40.07/otherpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesOtherPackage100));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithThreeItemsForSamePackage(string pageContent)
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(pageContent));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2017.02.08.16.49.48/mypackage.3.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageMyPackageCreated));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2017.02.08.16.49.59/mypackage.3.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageMyPackageUnlisted));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2017.02.08.17.16.18/mypackage.3.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageMyPackageListed));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithSemVer2Package()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithSemVer2Index));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithSemVer2Page000));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/testpackage.semver2.1.0.0-alpha.1.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithSemVer2Package));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithNonNormalizedDelete()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithNonNormalizedDeleteIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithNonNormalizedDeletePage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/otherpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithNonNormalizedDeleteOtherPackage100));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.13.06.40.07/otherpackage.1.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogWithNonNormalizedDeleteOtherPackageDelete));

            return catalogStorage;
        }

        public static MemoryStorage CreateTestCatalogWithOnePackage()
        {
            var catalogStorage = new MemoryStorage();

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "index.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithOnePackageIndex));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "page0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithOnePackagePage));

            catalogStorage.Content.TryAdd(
                new Uri(catalogStorage.BaseAddress, "data/2015.10.12.10.08.54/listedpackage.1.0.0.json"),
                new StringStorageContent(TestCatalogEntries.TestCatalogStorageWithThreePackagesListedPackage100));

            return catalogStorage;
        }
    }
}