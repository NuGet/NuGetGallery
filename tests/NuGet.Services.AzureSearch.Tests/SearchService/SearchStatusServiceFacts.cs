// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
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
                _searchIndex.Verify(x => x.CountAsync(), Times.Never);
                _searchIndex.Verify(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()), Times.Never);
                _hijackIndex.Verify(x => x.CountAsync(), Times.Never);
                _hijackIndex.Verify(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()), Times.Never);
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
                _searchIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()))
                    .ReturnsAsync(new SingleSearchResultPage<BaseMetadataDocument>(
                        new SearchResult<BaseMetadataDocument>[0],
                        count: 0));

                var status = await _target.GetStatusAsync(SearchStatusOptions.All, _assembly);

                Assert.True(status.Success);
                Assert.NotNull(status.SearchIndex);
                Assert.Null(status.SearchIndex.LastCommitTimestamp);
            }

            [Fact]
            public async Task ReturnsNullCommitTimestampWhenTheLastCommitTimestampIsNull()
            {
                _searchIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()))
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
                Assert.Equal(_lastSecretRefresh, status.Server.LastServiceRefreshTime);
                Assert.Equal(RuntimeInformation.FrameworkDescription, status.Server.FrameworkDescription);

                Assert.Equal(23, status.SearchIndex.DocumentCount);
                Assert.Equal("search-index", status.SearchIndex.Name);
                Assert.InRange(status.SearchIndex.WarmQueryDuration, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.Equal(_searchLastCommitTimestamp, status.SearchIndex.LastCommitTimestamp);

                Assert.Equal(42, status.HijackIndex.DocumentCount);
                Assert.Equal("hijack-index", status.HijackIndex.Name);
                Assert.InRange(status.HijackIndex.WarmQueryDuration, TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue);
                Assert.Equal(_hijackLastCommitTimestamp, status.HijackIndex.LastCommitTimestamp);

                Assert.Same(_auxiliaryFilesMetadata, status.AuxiliaryFiles);

                _searchIndex.Verify(x => x.CountAsync(), Times.Once);
                _searchIndex.Verify(x => x.SearchAsync<BaseMetadataDocument>("*", It.IsAny<SearchOptions>()), Times.Exactly(2));
                _hijackIndex.Verify(x => x.CountAsync(), Times.Once);
                _hijackIndex.Verify(x => x.SearchAsync<BaseMetadataDocument>("*", It.IsAny<SearchOptions>()), Times.Exactly(2));
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
                _searchIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()))
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
                _hijackIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>()))
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
            protected readonly Mock<ISearchClientWrapper> _searchIndex;
            protected readonly Mock<ISearchClientWrapper> _hijackIndex;
            protected readonly Mock<ISearchParametersBuilder> _parametersBuilder;
            protected readonly Mock<IAuxiliaryDataCache> _auxiliaryDataCache;
            protected readonly Mock<IAuxiliaryData> _auxiliaryData;
            protected readonly Mock<ISecretRefresher> _secretRefresher;
            protected readonly SearchServiceConfiguration _config;
            protected readonly Mock<IOptionsSnapshot<SearchServiceConfiguration>> _options;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<SearchStatusService> _logger;
            protected readonly AuxiliaryFilesMetadata _auxiliaryFilesMetadata;
            protected readonly Assembly _assembly;
            protected readonly SearchStatusService _target;
            protected readonly SearchOptions _lastCommitTimestampParameters;
            protected readonly DateTimeOffset _searchLastCommitTimestamp;
            protected readonly DateTimeOffset _hijackLastCommitTimestamp;
            protected readonly DateTimeOffset _lastSecretRefresh;

            public BaseFacts(ITestOutputHelper output)
            {
                _searchIndex = new Mock<ISearchClientWrapper>();
                _hijackIndex = new Mock<ISearchClientWrapper>();
                _parametersBuilder = new Mock<ISearchParametersBuilder>();
                _auxiliaryDataCache = new Mock<IAuxiliaryDataCache>();
                _auxiliaryData = new Mock<IAuxiliaryData>();
                _secretRefresher = new Mock<ISecretRefresher>();
                _options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<SearchStatusService>();

                _auxiliaryFilesMetadata = new AuxiliaryFilesMetadata(
                    DateTimeOffset.MinValue,
                    new AuxiliaryFileMetadata(
                        DateTimeOffset.MinValue,
                        TimeSpan.Zero,
                        0,
                        string.Empty),
                    new AuxiliaryFileMetadata(
                        DateTimeOffset.MinValue,
                        TimeSpan.Zero,
                        0,
                        string.Empty),
                    new AuxiliaryFileMetadata(
                        DateTimeOffset.MinValue,
                        TimeSpan.Zero,
                        0,
                        string.Empty));
                _assembly = typeof(BaseFacts).Assembly;
                _config = new SearchServiceConfiguration();
                _config.DeploymentLabel = "Fake deployment label.";
                _lastCommitTimestampParameters = new SearchOptions();
                _searchLastCommitTimestamp = new DateTimeOffset(2019, 7, 1, 0, 0, 0, TimeSpan.Zero);
                _hijackLastCommitTimestamp = new DateTimeOffset(2019, 7, 2, 0, 0, 0, TimeSpan.Zero);
                _lastSecretRefresh = new DateTimeOffset(2019, 7, 3, 0, 0, 0, TimeSpan.Zero);
                Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "Fake website instance ID.");

                _secretRefresher.Setup(x => x.LastRefresh).Returns(() => _lastSecretRefresh);
                _searchIndex.Setup(x => x.IndexName).Returns("search-index");
                _hijackIndex.Setup(x => x.IndexName).Returns("hijack-index");
                _searchIndex.Setup(x => x.CountAsync()).ReturnsAsync(23);
                _hijackIndex.Setup(x => x.CountAsync()).ReturnsAsync(42);
                _parametersBuilder.Setup(x => x.LastCommitTimestamp()).Returns(() => _lastCommitTimestampParameters);

                _searchIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.Is<SearchOptions>(p => !IsLastCommitTimestamp(p))))
                    .ReturnsAsync(new SingleSearchResultPage<BaseMetadataDocument>(
                        new SearchResult<BaseMetadataDocument>[0],
                        count: 0))
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));
                _searchIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.Is<SearchOptions>(p => IsLastCommitTimestamp(p))))
                    .ReturnsAsync(GetLastCommitTimestampResult(_searchLastCommitTimestamp));
                _hijackIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.Is<SearchOptions>(p => !IsLastCommitTimestamp(p))))
                    .ReturnsAsync(new SingleSearchResultPage<BaseMetadataDocument>(
                        new SearchResult<BaseMetadataDocument>[0],
                        count: 0))
                    .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(1)));
                _hijackIndex
                    .Setup(x => x.SearchAsync<BaseMetadataDocument>(It.IsAny<string>(), It.Is<SearchOptions>(p => IsLastCommitTimestamp(p))))
                    .ReturnsAsync(GetLastCommitTimestampResult(_hijackLastCommitTimestamp));
                _options.Setup(x => x.Value).Returns(() => _config);
                _auxiliaryDataCache.Setup(x => x.Get()).Returns(() => _auxiliaryData.Object);
                _auxiliaryData.Setup(x => x.Metadata).Returns(() => _auxiliaryFilesMetadata);

                _target = new SearchStatusService(
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _parametersBuilder.Object,
                    _auxiliaryDataCache.Object,
                    _secretRefresher.Object,
                    _options.Object,
                    _telemetryService.Object,
                    _logger);
            }

            protected static SingleSearchResultPage<BaseMetadataDocument> GetLastCommitTimestampResult(DateTimeOffset? timestamp)
            {
                return new SingleSearchResultPage<BaseMetadataDocument>(
                    new List<SearchResult<BaseMetadataDocument>>
                    {
                        SearchModelFactory.SearchResult(new BaseMetadataDocument
                        {
                            LastCommitTimestamp = timestamp,
                        }, null, null),
                    },
                    count: 1);
            }

            private bool IsLastCommitTimestamp(SearchOptions parameters)
            {
                return parameters == _lastCommitTimestampParameters;
            }
        }
    }
}
