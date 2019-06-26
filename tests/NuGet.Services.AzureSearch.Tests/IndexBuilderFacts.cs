// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGet.Services.AzureSearch.ScoringProfiles;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.AzureSearch.Support;
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
                _cloudBlobContainer.Verify(x => x.CreateAsync(), Times.Once);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
                VerifySetPermissions();
            }

            [Fact]
            public async Task CanRetryOnConflict()
            {
                EnableConflict();

                var sw = Stopwatch.StartNew();
                await _target.CreateAsync(retryOnConflict: true);
                sw.Stop();

                _cloudBlobContainer.Verify(x => x.CreateAsync(), Times.Exactly(2));
                VerifySetPermissions();
                Assert.InRange(sw.Elapsed, _retryDuration, TimeSpan.MaxValue);
            }

            [Fact]
            public async Task CanFailOnConflict()
            {
                EnableConflict();

                await Assert.ThrowsAsync<StorageException>(() => _target.CreateAsync(retryOnConflict: false));
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
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null, null)).ReturnsAsync(false);

                await _target.CreateIfNotExistsAsync();

                VerifyGetContainer();
                _cloudBlobContainer.Verify(x => x.CreateAsync(), Times.Once);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
                VerifySetPermissions();
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null, null)).ReturnsAsync(true);

                await _target.CreateIfNotExistsAsync();

                _cloudBlobContainer.Verify(x => x.CreateAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>()), Times.Never);
            }

            [Fact]
            public async Task DoesNotRetryOnConflict()
            {
                EnableConflict();
                _cloudBlobContainer.Setup(x => x.ExistsAsync(null, null)).ReturnsAsync(false);

                await Assert.ThrowsAsync<StorageException>(() => _target.CreateIfNotExistsAsync());
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
                _cloudBlobContainer.Verify(x => x.CreateAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(), Times.Never);
                _cloudBlobContainer.Verify(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>()), Times.Never);
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
                    .SetupSequence(x => x.CreateAsync())
                    .Throws(new StorageException(
                        new RequestResult
                        {
                            HttpStatusCode = (int)HttpStatusCode.Conflict,
                        },
                        "Conflict.",
                        inner: null))
                    .Returns(Task.CompletedTask);
            }

            protected void VerifySetPermissions()
            {
                _cloudBlobContainer.Verify(
                    x => x.SetPermissionsAsync(It.Is<BlobContainerPermissions>(p => p.PublicAccess == BlobContainerPublicAccessType.Blob)),
                    Times.Once);
                _cloudBlobContainer.Verify(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>()), Times.Once);
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

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.Is<Index>(y => y.Name == _config.SearchIndexName)),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
                    Times.Once);
            }

            [Fact]
            public async Task CreatesScoringProfile()
            {
                Index createdIndex = null;
                _indexesOperations
                    .Setup(o => o.CreateAsync(It.IsAny<Index>()))
                    .Callback<Index>(index => createdIndex = index)
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
                Assert.Equal(2, result.Functions.Count);
                var downloadsBoost = result
                    .Functions
                    .Where(f => f.FieldName == IndexFields.Search.DownloadScore)
                    .FirstOrDefault();
                var freshnessBoost = result
                    .Functions
                    .Where(f => f.FieldName == IndexFields.Published)
                    .FirstOrDefault();

                Assert.NotNull(downloadsBoost);
                Assert.Equal(5.0, downloadsBoost.Boost);

                Assert.NotNull(freshnessBoost);
                Assert.Equal(6.0, freshnessBoost.Boost);
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

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.Is<Index>(y => y.Name == _config.HijackIndexName)),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateScoringProfile()
            {
                Index createdIndex = null;
                _indexesOperations
                    .Setup(o => o.CreateAsync(It.IsAny<Index>()))
                    .Callback<Index>(index => createdIndex = index)
                    .Returns(() => Task.FromResult(createdIndex));

                await _target.CreateHijackIndexAsync();

                Assert.NotNull(createdIndex);
                Assert.Null(createdIndex.ScoringProfiles);
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
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.SearchIndexName))
                    .ReturnsAsync(false);

                await _target.CreateSearchIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.Is<Index>(y => y.Name == _config.SearchIndexName)),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.SearchIndexName))
                    .ReturnsAsync(true);

                await _target.CreateSearchIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
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
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.HijackIndexName))
                    .ReturnsAsync(false);

                await _target.CreateHijackIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.Is<Index>(y => y.Name == _config.HijackIndexName)),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotCreateIndexIfExists()
            {
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.HijackIndexName))
                    .ReturnsAsync(true);

                await _target.CreateHijackIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.CreateAsync(It.IsAny<Index>()),
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
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.SearchIndexName))
                    .ReturnsAsync(false);

                await _target.CreateSearchIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.DeleteAsync(It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesIndexIfExists()
            {
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.SearchIndexName))
                    .ReturnsAsync(true);

                await _target.DeleteSearchIndexIfExistsAsync();

                _indexesOperations.Verify(
                    x => x.DeleteAsync(_config.SearchIndexName),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.DeleteAsync(It.IsAny<string>()),
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
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.HijackIndexName))
                    .ReturnsAsync(false);

                await _target.CreateHijackIndexIfNotExistsAsync();

                _indexesOperations.Verify(
                    x => x.DeleteAsync(It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task DeletesIndexIfExists()
            {
                _indexesOperations
                    .Setup(x => x.ExistsAsync(_config.HijackIndexName))
                    .ReturnsAsync(true);

                await _target.DeleteHijackIndexIfExistsAsync();

                _indexesOperations.Verify(
                    x => x.DeleteAsync(_config.HijackIndexName),
                    Times.Once);
                _indexesOperations.Verify(
                    x => x.DeleteAsync(It.IsAny<string>()),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ISearchServiceClientWrapper> _serviceClient;
            protected readonly Mock<IIndexesOperationsWrapper> _indexesOperations;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly RecordingLogger<IndexBuilder> _logger;
            protected readonly IndexBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _serviceClient = new Mock<ISearchServiceClientWrapper>();
                _indexesOperations = new Mock<IIndexesOperationsWrapper>();
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
                        PublishedFreshnessBoost = 6.0
                    }
                };
                _logger = output.GetLogger<IndexBuilder>();

                _options
                    .Setup(x => x.Value)
                    .Returns(() => _config);
                _serviceClient
                    .Setup(x => x.Indexes)
                    .Returns(() => _indexesOperations.Object);

                _target = new IndexBuilder(
                    _serviceClient.Object,
                    _options.Object,
                    _logger);
            }
        }
    }
}
