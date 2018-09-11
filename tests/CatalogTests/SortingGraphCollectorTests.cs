// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;
using Xunit;

namespace CatalogTests
{
    public class SortingGraphCollectorTests
    {
        private MockServerHttpClientHandler _mockServer;
        private TestSortingGraphCollector _collector;

        private void Initialize()
        {
            _mockServer = new MockServerHttpClientHandler();
            _mockServer.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            _collector = new TestSortingGraphCollector(
                new Uri("http://tempuri.org/index.json"),
                new Uri[] { Schema.DataTypes.PackageDetails, Schema.DataTypes.PackageDelete },
                handlerFunc: () => _mockServer);
        }

        [Fact]
        public async Task AllGraphsAreReadOnly()
        {
            // Arrange
            Initialize();

            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorageAsync(catalogStorage);

            // Act
            var result = await _collector.RunAsync(CancellationToken.None);

            // Assert
            foreach (var graphs in _collector.AllSortedGraphs)
            {
                foreach (var pair in graphs.Value)
                {
                    Assert.IsType<ReadOnlyGraph>(pair.Value);
                }
            }

            Assert.True(result, "The result of running the test collector should be true.");
        }

        [Fact]
        public async Task GraphsAreBatchedById()
        {
            // Arrange
            Initialize();

            var catalogStorage = Catalogs.CreateTestCatalogWithCommitThenTwoPackageCommit();
            await _mockServer.AddStorageAsync(catalogStorage);

            // Act
            var result = await _collector.RunAsync(CancellationToken.None);

            // Assert
            Assert.Equal(
                new[] { "AnotherPackage", "ListedPackage", "UnlistedPackage" },
                _collector.AllSortedGraphs.Select(x => x.Key).OrderBy(x => x).ToArray());
        }

        private class TestSortingGraphCollector : SortingGraphCollector
        {
            private readonly ConcurrentBag<KeyValuePair<string, IReadOnlyDictionary<string, IGraph>>> _allSortedGraphs;

            public TestSortingGraphCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc) : base(
                index,
                types,
                new Mock<ITelemetryService>().Object,
                handlerFunc)
            {
                _allSortedGraphs = new ConcurrentBag<KeyValuePair<string, IReadOnlyDictionary<string, IGraph>>>();
            }

            public IEnumerable<KeyValuePair<string, IReadOnlyDictionary<string, IGraph>>> AllSortedGraphs => _allSortedGraphs;

            protected override Task ProcessGraphsAsync(KeyValuePair<string, IReadOnlyDictionary<string, IGraph>> sortedGraphs, CancellationToken cancellationToken)
            {
                _allSortedGraphs.Add(sortedGraphs);
                return Task.FromResult(0);
            }
        }
    }
}