// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogIndexReaderTests
    {
        [Fact]
        public async Task GetEntries()
        {
            // Arrange
            var indexUri = "http://tempuri.org/index.json";
            var responses = new Dictionary<string, string>()
            {
                { "http://tempuri.org/index.json", TestCatalogEntries.TestCatalogStorageWithThreePackagesIndex },
                { "http://tempuri.org/page0.json", TestCatalogEntries.TestCatalogStorageWithThreePackagesPage },
            };

            var reader = new CatalogIndexReader(
                new Uri(indexUri),
                new CollectorHttpClient(new InMemoryHttpHandler(responses)),
                new Mock<ITelemetryService>().Object);

            // Act
            var entries = await reader.GetEntries();

            // Assert
            var entryList = entries.ToList();
            Assert.Equal(3, entryList.Count);

            Assert.Equal("http://tempuri.org/data/2015.10.12.10.08.55/listedpackage.1.0.1.json", entryList[0].Uri.ToString());
            Assert.Equal("http://tempuri.org/data/2015.10.12.10.08.54/listedpackage.1.0.0.json", entryList[1].Uri.ToString());
            Assert.Equal("http://tempuri.org/data/2015.10.12.10.08.54/unlistedpackage.1.0.0.json", entryList[2].Uri.ToString());

            Assert.Equal("2015-10-12T10:08:55.3335317", entryList[0].CommitTimeStamp.ToString("O"));
            Assert.Equal("2015-10-12T10:08:54.1506742", entryList[1].CommitTimeStamp.ToString("O"));
            Assert.Equal("2015-10-12T10:08:54.1506742", entryList[2].CommitTimeStamp.ToString("O"));

            Assert.Equal("8a9e7694-73d4-4775-9b7a-20aa59b9773e", entryList[0].CommitId);
            Assert.Equal("9a37734f-1960-4c07-8934-c8bc797e35c1", entryList[1].CommitId);
            Assert.Equal("9a37734f-1960-4c07-8934-c8bc797e35c1", entryList[2].CommitId);

            Assert.Equal("ListedPackage", entryList[0].Id);
            Assert.Equal("ListedPackage", entryList[1].Id);
            Assert.Equal("UnlistedPackage", entryList[2].Id);

            Assert.Equal(new NuGetVersion("1.0.1"), entryList[0].Version);
            Assert.Equal(new NuGetVersion("1.0.0"), entryList[1].Version);
            Assert.Equal(new NuGetVersion("1.0.0"), entryList[2].Version);

            Assert.Equal(new[] { "nuget:PackageDetails" }, entryList[0].Types);
            Assert.Equal(new[] { "nuget:PackageDetails" }, entryList[1].Types);
            Assert.Equal(new[] { "nuget:PackageDetails" }, entryList[2].Types);

            Assert.Same(entryList[0].Id, entryList[1].Id);
        }
    }
}
