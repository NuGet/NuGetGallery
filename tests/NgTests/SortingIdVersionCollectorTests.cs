// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;
using Xunit;

namespace NgTests
{
    public class SortingIdVersionCollectorTests
    {
        public static IEnumerable<object[]> BatchesData
        {
            get
            {
                // Different id, same version
                yield return new object[]
                {
                    new List<CatalogCommitItem>
                    {
                        CreatePackage("test1", "1.0.0"),
                        CreatePackage("test2", "1.0.0"),
                        CreatePackage("test3", "2.0.0"),
                        CreatePackage("test4", "2.0.0")
                    }
                };

                // Same id, different version
                yield return new object[]
                {
                    new List<CatalogCommitItem>
                    {
                        CreatePackage("test1", "1.0.0"),
                        CreatePackage("test1", "2.0.0"),
                        CreatePackage("test1", "3.0.0"),
                        CreatePackage("test1", "4.0.0")
                    }
                };

                // Same id, same version
                yield return new object[]
                {
                    new List<CatalogCommitItem>
                    {
                        CreatePackage("test1", "1.0.0"),
                        CreatePackage("test1", "1.0.0"),
                        CreatePackage("test2", "2.0.0"),
                        CreatePackage("test2", "2.0.0")
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(BatchesData))]
        public async Task OnProcessBatch_BatchesCorrectly(IEnumerable<CatalogCommitItem> items)
        {
            // Arrange
            var collectorMock = new Mock<TestableSortingIdVersionCollector>()
            {
                CallBase = true
            };

            var seenPackages = new List<FeedPackageIdentity>();

            collectorMock
                .Setup(x => x.OverridableProcessSortedBatch(It.IsAny<KeyValuePair<FeedPackageIdentity, IList<CatalogCommitItem>>>()))
                .Returns<KeyValuePair<FeedPackageIdentity, IList<CatalogCommitItem>>>(
                    (pair) =>
                    {
                        // Assert
                        Assert.DoesNotContain(
                            seenPackages,
                            (p) =>
                            {
                                return p.Id == pair.Key.Id && p.Version == pair.Key.Version;
                            });

                        seenPackages.Add(new FeedPackageIdentity(pair.Key.Id, pair.Key.Version));

                        return Task.FromResult(0);
                    });

            // Act
            var result = await collectorMock.Object.OnProcessBatchAsync(items);
        }

        private static CatalogCommitItem CreatePackage(string id, string version)
        {
            var context = TestUtility.CreateCatalogContextJObject();
            var packageIdentity = new PackageIdentity(id, new NuGetVersion(version));
            var commitItem = TestUtility.CreateCatalogCommitItemJObject(DateTime.UtcNow, packageIdentity);

            return CatalogCommitItem.Create(context, commitItem);
        }

        public class TestableSortingIdVersionCollector : SortingIdVersionCollector
        {
            public TestableSortingIdVersionCollector()
                : base(
                    new Uri("https://nuget.test"),
                    Mock.Of<ITelemetryService>(),
                    handlerFunc: null)
            {
            }

            public Task<bool> OnProcessBatchAsync(IEnumerable<CatalogCommitItem> items)
            {
                return base.OnProcessBatchAsync(null, items, null, DateTime.MinValue, false, CancellationToken.None);
            }

            protected override Task ProcessSortedBatchAsync(
                CollectorHttpClient client,
                KeyValuePair<FeedPackageIdentity, IList<CatalogCommitItem>> sortedBatch,
                JToken context,
                CancellationToken cancellationToken)
            {
                return OverridableProcessSortedBatch(sortedBatch);
            }

            public virtual Task OverridableProcessSortedBatch(
                KeyValuePair<FeedPackageIdentity, IList<CatalogCommitItem>> sortedBatch)
            {
                return Task.FromResult(0);
            }
        }
    }
}