// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
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
            protected readonly Mock<IOptionsSnapshot<AzureSearchConfiguration>> _options;
            protected readonly AzureSearchConfiguration _config;
            protected readonly RecordingLogger<IndexBuilder> _logger;
            protected readonly IndexBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _serviceClient = new Mock<ISearchServiceClientWrapper>();
                _indexesOperations = new Mock<IIndexesOperationsWrapper>();
                _options = new Mock<IOptionsSnapshot<AzureSearchConfiguration>>();
                _config = new AzureSearchConfiguration
                {
                    SearchIndexName = "search",
                    HijackIndexName = "hijack",
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
