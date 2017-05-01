// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
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
                    new List<JToken>
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
                    new List<JToken>
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
                    new List<JToken>
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
        public async Task OnProcessBatch_BatchesCorrectly(IEnumerable<JToken> items)
        {
            // Arrange
            var collectorMock = new Mock<TestableSortingIdVersionCollector>()
            {
                CallBase = true
            };

            var seenPackages = new List<FeedPackageIdentity>();

            collectorMock
                .Setup(x => x.OverridableProcessSortedBatch(It.IsAny<KeyValuePair<FeedPackageIdentity, IList<JObject>>>()))
                .Returns<KeyValuePair<FeedPackageIdentity, IList<JObject>>>(
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
            var result = await collectorMock.Object.OnProcessBatch(items);
        }

        private static JToken CreatePackage(string id, string version)
        {
            return new JObject
            {
                { "nuget:id", id },
                { "nuget:version", version }
            };
        }

        public class TestableSortingIdVersionCollector : SortingIdVersionCollector
        {
            public TestableSortingIdVersionCollector() 
                : base(new Uri("https://www.microsoft.com"), null)
            {
            }

            public Task<bool> OnProcessBatch(IEnumerable<JToken> items)
            {
                return base.OnProcessBatch(null, items, null, DateTime.MinValue, false, CancellationToken.None);
            }

            protected override Task ProcessSortedBatch(
                CollectorHttpClient client, 
                KeyValuePair<FeedPackageIdentity, IList<JObject>> sortedBatch, 
                JToken context, 
                CancellationToken cancellationToken)
            {
                return OverridableProcessSortedBatch(sortedBatch);
            }

            public virtual Task OverridableProcessSortedBatch(
                KeyValuePair<FeedPackageIdentity, IList<JObject>> sortedBatch)
            {
                return Task.FromResult(0);
            }
        }
    }
}
