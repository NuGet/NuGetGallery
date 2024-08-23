// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.ScoringProfiles;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.AzureSearch.Wrappers;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class BlobContainerBuilderFacts
    {
        public class CreateAsync : BaseFacts
        {
            public CreateAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task CreatesIndex(bool retryOnConflict)
            {
                await _target.CreateAsync(retryOnConflict);

                VerifyGetContainer();
                _cloudBlobContainer.Verify(x => x.CreateAsync(false), Times.Once);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(It.IsAny<bool>()), Times.Never);
                _cloudBlobContainer.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
            }

            [Fact]
            public async Task CanRetryOnConflict()
            {
                EnableConflict();

                await _target.CreateAsync(retryOnConflict: true);

                _cloudBlobContainer.Verify(x => x.CreateAsync(false), Times.Exactly(2));
            }

            [Fact]
            public async Task CanFailOnConflict()
            {
                EnableConflict();

                await Assert.ThrowsAsync<CloudBlobConflictException>(() => _target.CreateAsync(retryOnConflict: false));
            }
        }

        public class CreateIfNotExistsAsync : BaseFacts
        {
            public CreateIfNotExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CreatesIndexIfNotExists()
            {
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null)).ReturnsAsync(false);

                await _target.CreateIfNotExistsAsync();

                VerifyGetContainer();
                _cloudBlobContainer.Verify(x => x.CreateAsync(false), Times.Once);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(It.IsAny<bool>()), Times.Never);
                _cloudBlobContainer.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null)).ReturnsAsync(true);

                await _target.CreateIfNotExistsAsync();

                _cloudBlobContainer.Verify(x => x.CreateAsync(It.IsAny<bool>()), Times.Never);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task DoesNotRetryOnConflict()
            {
                EnableConflict();
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null)).ReturnsAsync(false);

                await Assert.ThrowsAsync<CloudBlobConflictException>(() => _target.CreateIfNotExistsAsync());
            }
        }

        public class DeleteIfExistsAsync : BaseFacts
        {
            public DeleteIfExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task Deletes()
            {
                await _target.DeleteIfExistsAsync();

                VerifyGetContainer();
                _cloudBlobContainer.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
                _cloudBlobContainer.Verify(x => x.CreateAsync(It.IsAny<bool>()), Times.Never);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(It.IsAny<bool>()), Times.Never);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ICloudBlobClient> _cloudBlobClient;
            protected readonly Mock<ICloudBlobContainer> _cloudBlobContainer;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly RecordingLogger<BlobContainerBuilder> _logger;
            protected readonly TimeSpan _retryDuration;
            protected readonly BlobContainerBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _cloudBlobClient = new Mock<ICloudBlobClient>();
                _cloudBlobContainer = new Mock<ICloudBlobContainer>();
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _config = new AzureSearchJobConfiguration
                {
                    StorageContainer = "container-name",
                };
                _logger = output.GetLogger<BlobContainerBuilder>();
                _retryDuration = TimeSpan.FromMilliseconds(10);

                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);
                _cloudBlobClient
                    .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(() => _cloudBlobContainer.Object);

                _target = new BlobContainerBuilder(
                    _cloudBlobClient.Object,
                    _options.Object,
                    _logger,
                    _retryDuration);
            }

            protected void EnableConflict()
            {
                _cloudBlobContainer
                    .SetupSequence(x => x.CreateAsync(It.IsAny<bool>()))
                    .ThrowsAsync(new CloudBlobConflictException(null))
                    .Returns(Task.CompletedTask);
            }

            protected void VerifyGetContainer()
            {
                _cloudBlobClient.Verify(x => x.GetContainerReference(_config.StorageContainer), Times.Once);
                _cloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Once);
            }
        }
    }

    public class IndexBuilderFacts
    {
        public class CreateSearchIndexAsync : BaseFacts
        {
            public CreateSearchIndexAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CreatesIndex()
            {
                await _target.CreateSearchIndexAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.Is<SearchIndex>(y => y.Name == _config.SearchIndexName)),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Once);
            }

            [Fact]
            public async Task CreatesScoringProfile()
            {
                SearchIndex createdIndex = null;
                _serviceClient
                    .Setup(o => o.CreateIndexAsync(It.IsAny<SearchIndex>()))
                    .Callback<SearchIndex>(index => createdIndex = index)
                    .Returns(() => Task.FromResult(createdIndex));

                await _target.CreateSearchIndexAsync();

                Assert.NotNull(createdIndex);

                var result = Assert.Single(createdIndex.ScoringProfiles);
                Assert.Equal(DefaultScoringProfile.Name, result.Name);

                // Verify field weights
                Assert.Equal(2, result.TextWeights.Weights.Count);

                Assert.Contains(IndexFields.PackageId, result.TextWeights.Weights.Keys);
                Assert.Equal(3.0, result.TextWeights.Weights[IndexFields.PackageId]);

                Assert.Contains(IndexFields.TokenizedPackageId, result.TextWeights.Weights.Keys);
                Assert.Equal(4.0, result.TextWeights.Weights[IndexFields.TokenizedPackageId]);

                // Verify boosting functions
                Assert.Single(result.Functions);
                var downloadsBoost = result
                    .Functions
                    .Where(f => f.FieldName == IndexFields.Search.DownloadScore)
                    .FirstOrDefault();

                Assert.NotNull(downloadsBoost);
                Assert.Equal(5.0, downloadsBoost.Boost);
            }

            [Fact]
            public async Task ThrowsOnInvalidFieldInConfig()
            {
                _config.Scoring.FieldWeights["WARGLE"] = 123.0;

                var exception = await Assert.ThrowsAsync<ArgumentException>(() => _target.CreateSearchIndexAsync());

                Assert.Contains("Unknown field 'WARGLE'", exception.Message);
            }
        }

        public class CreateHijackIndexAsync : BaseFacts
        {
            public CreateHijackIndexAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CreatesIndex()
            {
                await _target.CreateHijackIndexAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.Is<SearchIndex>(y => y.Name == _config.HijackIndexName)),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateScoringProfile()
            {
                SearchIndex createdIndex = null;
                _serviceClient
                    .Setup(o => o.CreateIndexAsync(It.IsAny<SearchIndex>()))
                    .Callback<SearchIndex>(index => createdIndex = index)
                    .Returns(() => Task.FromResult(createdIndex));

                await _target.CreateHijackIndexAsync();

                Assert.NotNull(createdIndex);
                Assert.Empty(createdIndex.ScoringProfiles);
            }
        }

        public class CreateSearchIndexIfNotExistsAsync : BaseFacts
        {
            public CreateSearchIndexIfNotExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CreatesIndexIfNotExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.SearchIndexName))
                    .ThrowsAsync(new RequestFailedException(404, ""));

                await _target.CreateSearchIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.Is<SearchIndex>(y => y.Name == _config.SearchIndexName)),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.SearchIndexName))
                    .ReturnsAsync(new SearchIndex(_config.SearchIndexName));

                await _target.CreateSearchIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Never);
            }
        }

        public class CreateHijackIndexIfNotExistsAsync : BaseFacts
        {
            public CreateHijackIndexIfNotExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CreatesIndexIfNotExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.HijackIndexName))
                    .ThrowsAsync(new RequestFailedException(404, ""));

                await _target.CreateHijackIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.Is<SearchIndex>(y => y.Name == _config.HijackIndexName)),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.HijackIndexName))
                    .ReturnsAsync(new SearchIndex(_config.HijackIndexName));

                await _target.CreateHijackIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.CreateIndexAsync(It.IsAny<SearchIndex>()),
                    Times.Never);
            }
        }

        public class DeleteSearchIndexIfExistsAsync : BaseFacts
        {
            public DeleteSearchIndexIfExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotDeleteIndexIfNotExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.SearchIndexName))
                    .ThrowsAsync(new RequestFailedException(404, ""));

                await _target.CreateSearchIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesIndexIfExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.SearchIndexName))
                    .ReturnsAsync(new SearchIndex(_config.SearchIndexName));

                await _target.DeleteSearchIndexIfExistsAsync();

                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(_config.SearchIndexName),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(It.IsAny<string>()),
                    Times.Once);
            }
        }

        public class DeleteHijackIndexIfExistsAsync : BaseFacts
        {
            public DeleteHijackIndexIfExistsAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotDeleteIndexIfNotExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.HijackIndexName))
                    .ThrowsAsync(new RequestFailedException(404, ""));

                await _target.CreateHijackIndexIfNotExistsAsync();

                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesIndexIfExists()
            {
                _serviceClient
                    .Setup(x => x.GetIndexAsync(_config.HijackIndexName))
                    .ReturnsAsync(new SearchIndex(_config.HijackIndexName));

                await _target.DeleteHijackIndexIfExistsAsync();

                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(_config.HijackIndexName),
                    Times.Once);
                _serviceClient.Verify(
                    x => x.DeleteIndexAsync(It.IsAny<string>()),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ISearchIndexClientWrapper> _serviceClient;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly RecordingLogger<IndexBuilder> _logger;
            protected readonly IndexBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _serviceClient = new Mock<ISearchIndexClientWrapper>();
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _config = new AzureSearchJobConfiguration
                {
                    SearchIndexName = "search",
                    HijackIndexName = "hijack",

                    Scoring = new AzureSearchScoringConfiguration
                    {
                        FieldWeights = new Dictionary<string, double>
                        {
                            { nameof(IndexFields.PackageId), 3.0 },
                            { nameof(IndexFields.TokenizedPackageId), 4.0 },
                        },

                        DownloadScoreBoost = 5.0,
                    }
                };
                _logger = output.GetLogger<IndexBuilder>();

                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);

                _target = new IndexBuilder(
                    _serviceClient.Object,
                    _options.Object,
                    _logger);
            }
        }
    }
}
