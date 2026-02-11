// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Dnx
{
    public class DnxPackageVersionIndexCacheControlTests
    {
        [Fact]
        public async Task LoadPackageIdsToIncludeAsync_BlobDoesNotExist()
        {
            var storage = new Mock<IStorage>();
            storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

            DnxPackageVersionIndexCacheControl.PackageIdsToInclude = new HashSet<string>();

            await DnxPackageVersionIndexCacheControl.LoadPackageIdsToIncludeAsync(storage.Object, Mock.Of<ILogger>(), It.IsAny<CancellationToken>());

            Assert.Empty(DnxPackageVersionIndexCacheControl.PackageIdsToInclude);
        }

        [Theory]
        [InlineData("{\"ids\":[]}", 0)]
        [InlineData("{\"ids\":[\"PackageId1\",\"packageid1\"]}", 1)]
        [InlineData("{\"ids\":[\"PackageId1\",\"PackageId2\"]}", 2)]
        public async Task LoadPackageIdsToIncludeAsync_BlobExists(string json, int count)
        {
            var storage = new Mock<IStorage>();
            storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
            storage.Setup(x => x.LoadStringAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(json);

            DnxPackageVersionIndexCacheControl.PackageIdsToInclude = new HashSet<string>();

            await DnxPackageVersionIndexCacheControl.LoadPackageIdsToIncludeAsync(storage.Object, Mock.Of<ILogger>(), It.IsAny<CancellationToken>());

            Assert.Equal(count, DnxPackageVersionIndexCacheControl.PackageIdsToInclude.Count);
        }

        [Fact]
        public void GetCacheControl()
        {
            DnxPackageVersionIndexCacheControl.PackageIdsToInclude = new HashSet<string>() { "packageid1" };

            Assert.Equal("max-age=10", DnxPackageVersionIndexCacheControl.GetCacheControl("packageid1", Mock.Of<ILogger>()));
            Assert.Equal(Constants.NoStoreCacheControl, DnxPackageVersionIndexCacheControl.GetCacheControl("packageid2", Mock.Of<ILogger>()));
        }
    }
}
