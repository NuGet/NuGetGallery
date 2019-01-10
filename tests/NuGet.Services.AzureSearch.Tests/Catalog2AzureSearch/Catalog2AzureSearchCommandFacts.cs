// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class Catalog2AzureSearchCommandFacts
    {
        public class ExecuteAsync : BaseFacts
        {
            public ExecuteAsync(ITestOutputHelper output) : base(output)
            {
            }
            
            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task ObservesCreateContainersAndIndexesOption(bool shouldCreate, bool containerExists)
            {
                _cloudBlobContainer.Setup(x => x.ExistsAsync()).ReturnsAsync(containerExists);
                _config.CreateContainersAndIndexes = shouldCreate;
                var createIfExistsTimes = shouldCreate ? Times.Once() : Times.Never();
                var createTimes = shouldCreate && !containerExists ? Times.Once() : Times.Never();

                await _target.ExecuteAsync();

                _cloudBlobClient.Verify(x => x.GetContainerReference(_config.StorageContainer), createIfExistsTimes);
                _cloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), createIfExistsTimes);
                _cloudBlobContainer.Verify(x => x.ExistsAsync(), createIfExistsTimes);
                _cloudBlobContainer.Verify(x => x.CreateAsync(), createTimes);
                _indexBuilder.Verify(x => x.CreateSearchIndexIfNotExistsAsync(), createIfExistsTimes);
                _indexBuilder.Verify(x => x.CreateHijackIndexIfNotExistsAsync(), createIfExistsTimes);
            }

            [Fact]
            public async Task UsesCurrentCursorValueForFront()
            {
                var front = new DateTime(2019, 1, 3, 0, 0, 0);
                _storage.CursorValue = front;

                await _target.ExecuteAsync();

                _collector.Verify(
                    x => x.RunAsync(
                        It.Is<ReadWriteCursor>(c => c.Value == front),
                        It.IsAny<ReadCursor>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task UsesMaxForCursorWithNoDependencies()
            {
                _config.DependencyCursorUrls = null;

                await _target.ExecuteAsync();

                _collector.Verify(
                    x => x.RunAsync(
                        It.IsAny<ReadWriteCursor>(),
                        It.Is<ReadCursor>(c => c.Value == DateTime.MaxValue),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task UsesMinOfDependencyUrlsForBack()
            {
                var cursor1 = "https://example/dep-cursor/1.json";
                var cursorValue1 = new DateTime(2020, 1, 1);
                var cursor2 = "https://example/dep-cursor/2.json";
                var cursorValue2 = new DateTime(2020, 1, 2);
                _config.DependencyCursorUrls = new List<string> { cursor1, cursor2 };
                _httpMessageHandler
                    .Setup(x => x.OnSendAsync(
                        It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == cursor1),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(new Cursor {  Value = cursorValue1 }))
                    });
                _httpMessageHandler
                    .Setup(x => x.OnSendAsync(
                        It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == cursor2),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(new Cursor { Value = cursorValue2 }))
                    });

                await _target.ExecuteAsync();

                _collector.Verify(
                    x => x.RunAsync(
                        It.IsAny<ReadWriteCursor>(),
                        It.Is<ReadCursor>(c => c.Value == cursorValue1),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICollector> _collector;
            protected readonly Mock<IStorageFactory> _storageFactory;
            protected readonly Mock<TestHttpMessageHandler> _httpMessageHandler;
            protected readonly Mock<ICloudBlobClient> _cloudBlobClient;
            protected readonly Mock<ICloudBlobContainer> _cloudBlobContainer;
            protected readonly Mock<IIndexBuilder> _indexBuilder;
            protected readonly Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>> _options;
            protected readonly Catalog2AzureSearchConfiguration _config;
            protected readonly TestCursorStorage _storage;
            protected readonly RecordingLogger<Catalog2AzureSearchCommand> _logger;
            protected readonly Catalog2AzureSearchCommand _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _collector = new Mock<ICollector>();
                _storageFactory = new Mock<IStorageFactory>();
                _httpMessageHandler = new Mock<TestHttpMessageHandler>() { CallBase = true };
                _cloudBlobClient = new Mock<ICloudBlobClient>();
                _cloudBlobContainer = new Mock<ICloudBlobContainer>();
                _indexBuilder = new Mock<IIndexBuilder>();
                _options = new Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>();
                _logger = output.GetLogger<Catalog2AzureSearchCommand>();

                _config = new Catalog2AzureSearchConfiguration
                {
                    StorageConnectionString = "UseDevelopmentStorage=true",
                    StorageContainer = "container-name",
                };
                _storage = new TestCursorStorage(new Uri("https://example/base/"));

                _options.Setup(x => x.Value).Returns(() => _config);
                _storageFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(() => _storage);
                _cloudBlobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => _cloudBlobContainer.Object);

                _target = new Catalog2AzureSearchCommand(
                    _collector.Object,
                    _storageFactory.Object,
                    () => _httpMessageHandler.Object,
                    _cloudBlobClient.Object,
                    _indexBuilder.Object,
                    _options.Object,
                    _logger);
            }
        }
    }
}
