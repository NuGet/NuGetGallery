// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchStatusServiceFacts
    {
        public class GetStatusAsync : BaseFacts
        {
            public GetStatusAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsSkippingAzureSearch()
            {
                var status = await _target.GetStatusAsync(SearchStatusOptions.All ^ SearchStatusOptions.AzureSearch, _assembly);

                Assert.True(status.Success);
                _searchDocuments.Verify(x => x.CountAsync(), Times.Never);
                _searchDocuments.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()), Times.Never);
                _hijackDocuments.Verify(x => x.CountAsync(), Times.Never);
                _hijackDocuments.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()), Times.Never);
                Assert.Null(status.SearchIndex);
                Assert.Null(status.HijackIndex);
                Assert.NotNull(status.Server);
                Assert.NotNull(status.AuxiliaryFiles);
            }

            [Fact]
            public async Task AllowsSkippingAuxiliaryFiles()
            {
                var status = await _target.GetStatusAsync(SearchStatusOptions.All ^ SearchStatusOptions.AuxiliaryFiles, _assembly);

                Assert.True(status.Success);
                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Never);
                _auxiliaryDataCache.Verify(x => x.Get(), Times.Never);
                Assert.Null(status.AuxiliaryFiles);
                Assert.NotNull(status.Server);
                Assert.NotNull(status.SearchIndex);
                Assert.NotNull(status.HijackIndex);
            }

            [Fact]
            public async Task AllowsSkippingServer()
            {
                var status = await _target.GetStatusAsync(SearchStatusOptions.All ^ SearchStatusOptions.Server, _assembly);

                Assert.True(status.Success);
                Assert.Null(status.Server);
                Assert.NotNull(status.AuxiliaryFiles);
                Assert.NotNull(status.SearchIndex);
                Assert.NotNull(status.HijackIndex);
            }

            [Fact]
            public async Task ReturnsNullCommitTimestampWhenThereAreNoDocuments()
            {
                _searchDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ReturnsAsync(new DocumentSearchResult());

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.True(status.Success);
                Assert.NotNull(status.SearchIndex);
                Assert.Null(status.SearchIndex.LastCommitTimestamp);
            }

            [Fact]
            public async Task ReturnsNullCommitTimestampWhenTheLastCommitTimestampIsNull()
            {
                _searchDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ReturnsAsync(GetLastCommitTimestampResult(timestamp: null));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.True(status.Success);
                Assert.NotNull(status.SearchIndex);
                Assert.Null(status.SearchIndex.LastCommitTimestamp);
            }

            [Fact]
            public async Task ReturnsFullStatus()
            {
                var before = DateTimeOffset.UtcNow;
                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.True(status.Success);
                Assert.InRange(status.Duration.Value, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);

                Assert.Equal("This is a fake build date for testing.", status.Server.AssemblyBuildDateUtc);
                Assert.Equal("This is a fake commit ID for testing.", status.Server.AssemblyCommitId);
                Assert.Equal("1.0.0-fakefortesting", status.Server.AssemblyInformationalVersion);
                Assert.Equal(_config.DeploymentLabel, status.Server.DeploymentLabel);
                Assert.Equal("Fake website instance ID.", status.Server.InstanceId);
                Assert.Equal(Environment.MachineName, status.Server.MachineName);
                Assert.InRange(status.Server.ProcessDuration, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.NotEqual(0, status.Server.ProcessId);
                Assert.InRange(status.Server.ProcessStartTime, DateTimeOffset.MinValue, before);

                Assert.Equal(23, status.SearchIndex.DocumentCount);
                Assert.Equal("search-index", status.SearchIndex.Name);
                Assert.InRange(status.SearchIndex.WarmQueryDuration, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.Equal(_searchLastCommitTimestamp, status.SearchIndex.LastCommitTimestamp);

                Assert.Equal(42, status.HijackIndex.DocumentCount);
                Assert.Equal("hijack-index", status.HijackIndex.Name);
                Assert.InRange(status.HijackIndex.WarmQueryDuration, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.Equal(_hijackLastCommitTimestamp, status.HijackIndex.LastCommitTimestamp);

                Assert.Same(_auxiliaryFilesMetadata, status.AuxiliaryFiles);

                _searchDocuments.Verify(x => x.CountAsync(), Times.Once);
                _searchDocuments.Verify(x => x.SearchAsync("*", It.IsAny<SearchParameters>()), Times.Exactly(2));
                _hijackDocuments.Verify(x => x.CountAsync(), Times.Once);
                _hijackDocuments.Verify(x => x.SearchAsync("*", It.IsAny<SearchParameters>()), Times.Exactly(2));
            }

            [Fact]
            public async Task HandlesFailedServerStatus()
            {
                _options.Setup(x => x.Value).Throws(new InvalidOperationException("Can't get the deployment label."));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.False(status.Success);
                Assert.InRange(status.Duration.Value, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.Null(status.Server);
                Assert.NotNull(status.SearchIndex);
                Assert.NotNull(status.HijackIndex);
                Assert.NotNull(status.AuxiliaryFiles);
            }

            [Fact]
            public async Task HandlesFailedSearchIndexStatus()
            {
                _searchDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ThrowsAsync(new InvalidOperationException("Could not hit the search index."));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.False(status.Success);
                Assert.InRange(status.Duration.Value, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.NotNull(status.Server);
                Assert.Null(status.SearchIndex);
                Assert.NotNull(status.HijackIndex);
                Assert.NotNull(status.AuxiliaryFiles);
            }

            [Fact]
            public async Task HandlesFailedHijackIndexStatus()
            {
                _hijackDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchParameters>()))
                    .ThrowsAsync(new InvalidOperationException("Could not hit the hijack index."));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.False(status.Success);
                Assert.InRange(status.Duration.Value, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.NotNull(status.Server);
                Assert.NotNull(status.SearchIndex);
                Assert.Null(status.HijackIndex);
                Assert.NotNull(status.AuxiliaryFiles);
            }

            [Fact]
            public async Task HandlesFailedAuxiliaryData()
            {
                _auxiliaryDataCache
                    .Setup(x => x.EnsureInitializedAsync())
                    .Throws(new InvalidOperationException("Could not initialize the auxiliary data."));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.False(status.Success);
                Assert.InRange(status.Duration.Value, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.NotNull(status.Server);
                Assert.NotNull(status.SearchIndex);
                Assert.NotNull(status.HijackIndex);
                Assert.Null(status.AuxiliaryFiles);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ISearchIndexClientWrapper> _searchIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _searchDocuments;
            protected readonly Mock<ISearchIndexClientWrapper> _hijackIndex;
            protected readonly Mock<IDocumentsOperationsWrapper> _hijackDocuments;
            protected readonly Mock<ISearchParametersBuilder> _parametersBuilder;
            protected readonly Mock<IAuxiliaryDataCache> _auxiliaryDataCache;
            protected readonly Mock<IAuxiliaryData> _auxiliaryData;
            protected readonly SearchServiceConfiguration _config;
            protected readonly Mock<IOptionsSnapshot<SearchServiceConfiguration>> _options;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<SearchStatusService> _logger;
            protected readonly AuxiliaryFilesMetadata _auxiliaryFilesMetadata;
            protected readonly Assembly _assembly;
            protected readonly SearchStatusService _target;
            protected readonly SearchParameters _lastCommitTimestampParameters;
            protected readonly DateTimeOffset _searchLastCommitTimestamp;
            protected readonly DateTimeOffset _hijackLastCommitTimestamp;

            public BaseFacts(ITestOutputHelper output)
            {
                _searchIndex = new Mock<ISearchIndexClientWrapper>();
                _searchDocuments = new Mock<IDocumentsOperationsWrapper>();
                _hijackIndex = new Mock<ISearchIndexClientWrapper>();
                _hijackDocuments = new Mock<IDocumentsOperationsWrapper>();
                _parametersBuilder = new Mock<ISearchParametersBuilder>();
                _auxiliaryDataCache = new Mock<IAuxiliaryDataCache>();
                _auxiliaryData = new Mock<IAuxiliaryData>();
                _options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<SearchStatusService>();

                _auxiliaryFilesMetadata = new AuxiliaryFilesMetadata(
                    new AuxiliaryFileMetadata(
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MinValue,
                        TimeSpan.Zero,
                        0,
                        string.Empty),
                    new AuxiliaryFileMetadata(
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MinValue,
                        TimeSpan.Zero,
                        0,
                        string.Empty));
                _assembly = typeof(BaseFacts).Assembly;
                _config = new SearchServiceConfiguration();
                _config.DeploymentLabel = "Fake deployment label.";
                _lastCommitTimestampParameters = new SearchParameters();
                _searchLastCommitTimestamp = new DateTimeOffset(2019, 7, 1, 0, 0, 0, TimeSpan.Zero);
                _hijackLastCommitTimestamp = new DateTimeOffset(2019, 7, 2, 0, 0, 0, TimeSpan.Zero);
                Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "Fake website instance ID.");

                _searchIndex.Setup(x => x.IndexName).Returns("search-index");
                _hijackIndex.Setup(x => x.IndexName).Returns("hijack-index");
                _searchIndex.Setup(x => x.Documents).Returns(() => _searchDocuments.Object);
                _hijackIndex.Setup(x => x.Documents).Returns(() => _hijackDocuments.Object);
                _searchDocuments.Setup(x => x.CountAsync()).ReturnsAsync(23);
                _hijackDocuments.Setup(x => x.CountAsync()).ReturnsAsync(42);
                _parametersBuilder.Setup(x => x.LastCommitTimestamp()).Returns(() => _lastCommitTimestampParameters);

                _searchDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.Is<SearchParameters>(p => !IsLastCommitTimestamp(p))))
                    .ReturnsAsync(new DocumentSearchResult())
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));
                _searchDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.Is<SearchParameters>(p => IsLastCommitTimestamp(p))))
                    .ReturnsAsync(GetLastCommitTimestampResult(_searchLastCommitTimestamp));
                _hijackDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.Is<SearchParameters>(p => !IsLastCommitTimestamp(p))))
                    .ReturnsAsync(new DocumentSearchResult())
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));
                _hijackDocuments
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.Is<SearchParameters>(p => IsLastCommitTimestamp(p))))
                    .ReturnsAsync(GetLastCommitTimestampResult(_hijackLastCommitTimestamp));
                _options.Setup(x => x.Value).Returns(() => _config);
                _auxiliaryDataCache.Setup(x => x.Get()).Returns(() => _auxiliaryData.Object);
                _auxiliaryData.Setup(x => x.Metadata).Returns(() => _auxiliaryFilesMetadata);

                _target = new SearchStatusService(
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _parametersBuilder.Object,
                    _auxiliaryDataCache.Object,
                    _options.Object,
                    _telemetryService.Object,
                    _logger);
            }

            protected static DocumentSearchResult GetLastCommitTimestampResult(DateTimeOffset? timestamp)
            {
                return new DocumentSearchResult
                {
                    Results = new List<SearchResult>
                    {
                        new SearchResult
                        {
                            Document = new Document
                            {
                                { "lastCommitTimestamp", timestamp },
                            },
                        },
                    },
                };
            }

            private bool IsLastCommitTimestamp(SearchParameters parameters)
            {
                return parameters == _lastCommitTimestampParameters;
            }
        }
    }
}
