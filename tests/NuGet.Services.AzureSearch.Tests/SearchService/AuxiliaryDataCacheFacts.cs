// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Indexing;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryDataCacheFacts
    {
        public class EnsureInitialized : BaseFacts
        {
            public EnsureInitialized(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void DefaultsToFalse()
            {
                Assert.False(_target.Initialized);
            }
        }

        public class InitializeAsync : BaseFacts
        {
            public InitializeAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task InitializesWhenUninitialized()
            {
                await _target.EnsureInitializedAsync();

                Assert.True(_target.Initialized);
                _client.Verify(x => x.LoadDownloadsAsync(null), Times.Once);
                _client.Verify(x => x.LoadDownloadsAsync(It.IsAny<string>()), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(null), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()), Times.Once);
                var message = Assert.Single(_logger.Messages.Where(x => x.Contains("Done reloading auxiliary data.")));
                Assert.EndsWith("Not modified: ", message);
                Assert.Contains("Reloaded: Downloads, VerifiedPackages", message);
            }

            [Fact]
            public async Task DoesNotInitializeAgainWhenAlreadyInitialized()
            {
                // Arrange
                await _target.EnsureInitializedAsync();
                _client.Invocations.Clear();

                // Act
                await _target.EnsureInitializedAsync();

                // Assert
                Assert.True(_target.Initialized);
                _client.Verify(x => x.LoadDownloadsAsync(It.IsAny<string>()), Times.Never);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task DoesNotInitializeAgainWhenCalledDuringInitialize()
            {
                // Arrange
                var downloadsTcs = new TaskCompletionSource<AuxiliaryFileResult<Downloads>>();
                var startedDownloadTcs = new TaskCompletionSource<bool>();
                _client
                    .Setup(x => x.LoadDownloadsAsync(It.IsAny<string>()))
                    .Returns(async () =>
                    {
                        startedDownloadTcs.TrySetResult(true);
                        return await downloadsTcs.Task;
                    });
                var otherTask = _target.EnsureInitializedAsync();

                // Act
                var thisTask = _target.EnsureInitializedAsync();
                await startedDownloadTcs.Task;
                downloadsTcs.TrySetResult(_downloads);
                await thisTask;

                // Assert
                Assert.True(_target.Initialized);
                _client.Verify(x => x.LoadDownloadsAsync(It.IsAny<string>()), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()), Times.Once);
            }
        }

        public class TryLoadAsync : BaseFacts
        {
            public TryLoadAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task InitializesWhenUninitialized()
            {
                await _target.TryLoadAsync(_token);

                Assert.True(_target.Initialized);
                _client.Verify(x => x.LoadDownloadsAsync(null), Times.Once);
                _client.Verify(x => x.LoadDownloadsAsync(It.IsAny<string>()), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(null), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()), Times.Once);
            }

            [Fact]
            public async Task InitializesAgainWhenAlreadyInitialized()
            {
                // Arrange
                await _target.TryLoadAsync(_token);
                _client.Invocations.Clear();

                // Act
                await _target.TryLoadAsync(_token);

                // Assert
                Assert.True(_target.Initialized);
                _client.Verify(x => x.LoadDownloadsAsync("downloads-etag"), Times.Once);
                _client.Verify(x => x.LoadDownloadsAsync(It.IsAny<string>()), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync("verified-packages-etag"), Times.Once);
                _client.Verify(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()), Times.Once);
            }
        }

        public class Get : BaseFacts
        {
            public Get(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void ThrowsWhenNotInitialized()
            {
                var ex = Assert.Throws<InvalidOperationException>(() => _target.Get());
                Assert.False(_target.Initialized);
                Assert.Equal("The auxiliary data has not been loaded yet. Call LoadAsync.", ex.Message);
            }

            [Fact]
            public async Task ReturnsDataWhenInitialized()
            {
                await _target.EnsureInitializedAsync();
                
                var value = _target.Get();

                Assert.True(_target.Initialized);
                Assert.NotNull(value);
                Assert.Same(_downloads.Metadata, value.Metadata.Downloads);
                Assert.Same(_verifiedPackages.Metadata, value.Metadata.VerifiedPackages);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IAuxiliaryFileClient> _client;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<AuxiliaryDataCache> _logger;
            protected readonly CancellationToken _token;
            protected readonly AuxiliaryFileResult<Downloads> _downloads;
            protected readonly AuxiliaryFileResult<HashSet<string>> _verifiedPackages;
            protected readonly AuxiliaryDataCache _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _client = new Mock<IAuxiliaryFileClient>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<AuxiliaryDataCache>();

                _token = CancellationToken.None;
                _downloads = new AuxiliaryFileResult<Downloads>(
                    notModified: false,
                    data: new Downloads(),
                    metadata: new AuxiliaryFileMetadata(
                        lastModified: DateTimeOffset.MinValue,
                        loaded: DateTimeOffset.MinValue,
                        loadDuration: TimeSpan.Zero,
                        fileSize: 0,
                        etag: "downloads-etag"));
                _verifiedPackages = new AuxiliaryFileResult<HashSet<string>>(
                    notModified: false,
                    data: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    metadata: new AuxiliaryFileMetadata(
                        lastModified: DateTimeOffset.MinValue,
                        loaded: DateTimeOffset.MinValue,
                        loadDuration: TimeSpan.Zero,
                        fileSize: 0,
                        etag: "verified-packages-etag"));

                _client
                    .Setup(x => x.LoadDownloadsAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => _downloads);
                _client
                    .Setup(x => x.LoadVerifiedPackagesAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => _verifiedPackages);

                _target = new AuxiliaryDataCache(
                    _client.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}
