// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitItemBatchTests
    {
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity(
            id: "a",
            version: new NuGetVersion("1.0.0"));

        [Fact]
        public void Constructor_WhenItemsIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitItemBatch(items: null));

            Assert.Equal("items", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenItemsIsEmpty_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitItemBatch(Enumerable.Empty<CatalogCommitItem>()));

            Assert.Equal("items", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("a")]
        public void Constructor_WhenCommitsAreUnordered_OrdersCommitsInChronologicallyAscendingOrder(string key)
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commitItem0 = TestUtility.CreateCatalogCommitItem(commitTimeStamp, _packageIdentity);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(1), _packageIdentity);
            var commitItem2 = TestUtility.CreateCatalogCommitItem(commitTimeStamp.AddMinutes(2), _packageIdentity);

            var commitItemBatch = new CatalogCommitItemBatch(
                new[] { commitItem1, commitItem0, commitItem2 },
                key);

            Assert.Equal(commitTimeStamp, commitItemBatch.CommitTimeStamp.ToUniversalTime());
            Assert.Equal(3, commitItemBatch.Items.Count);
            Assert.Equal(key, commitItemBatch.Key);
            Assert.Same(commitItem0, commitItemBatch.Items[0]);
            Assert.Same(commitItem1, commitItemBatch.Items[1]);
            Assert.Same(commitItem2, commitItemBatch.Items[2]);
        }
    }
}