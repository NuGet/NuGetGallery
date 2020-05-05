// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
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
                VerifyReadWithNoETag();
                var message = Assert.Single(_logger.Messages.Where(x => x.Contains("Done reloading auxiliary data.")));
                Assert.EndsWith("Not modified: ", message);
                Assert.Contains("Reloaded: Downloads, VerifiedPackages", message);
            }

            [Fact]
            public async Task DoesNotInitializeAgainWhenAlreadyInitialized()
            {
                // Arrange
                await _target.EnsureInitializedAsync();
                _downloadDataClient.Invocations.Clear();
                _verifiedPackagesDataClient.Invocations.Clear();

                // Act
                await _target.EnsureInitializedAsync();

                // Assert
                Assert.True(_target.Initialized);
                VerifyNoRead();
            }

            [Fact]
            public async Task DoesNotInitializeAgainWhenCalledDuringInitialize()
            {
                // Arrange
                var downloadsTcs = new TaskCompletionSource<AuxiliaryFileResult<DownloadData>>();
                var startedDownloadTcs = new TaskCompletionSource<bool>();
                _downloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .Returns(async () =>
                    {
                        startedDownloadTcs.TrySetResult(true);
                        return await downloadsTcs.Task;
                    });
                var otherTask = _target.EnsureInitializedAsync();

                // Act
                var thisTask = _target.EnsureInitializedAsync();
                await startedDownloadTcs.Task;
                downloadsTcs.TrySetResult(_downloadData);
                await thisTask;

                // Assert
                Assert.True(_target.Initialized);
                VerifyReadWithNoETag();
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
                VerifyReadWithNoETag();
            }

            [Fact]
            public async Task InitializesAgainWhenAlreadyInitialized()
            {
                // Arrange
                await _target.TryLoadAsync(_token);
                _downloadDataClient.Invocations.Clear();
                _verifiedPackagesDataClient.Invocations.Clear();

                // Act
                await _target.TryLoadAsync(_token);

                // Assert
                Assert.True(_target.Initialized);
                VerifyReadWithETag();
            }

            [Fact]
            public async Task ResetsStringCacheCounts()
            {
                // Perform two auxiliary file loads and verify the cache numbers emitted to telemetry.
                var invocations = 0;
                _downloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => _downloadData)
                    .Callback<IAccessCondition, StringCache>((_, sc) =>
                    {
                        invocations++;
                        sc.Dedupe(new string('a', 1));     // 1: miss   2: hit
                        sc.Dedupe(new string('a', 1));     // 1: hit    2: hit
                        sc.Dedupe(new string('b', 1));     // 1: miss   2: hit
                        if (invocations > 1)
                        {
                            sc.Dedupe(new string('a', 1)); // 1: n/a    2: hit
                            sc.Dedupe(new string('d', 1)); // 1: n/a    2: miss
                        }
                    });
                _verifiedPackagesDataClient
                    .Setup(x => x.ReadLatestAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => _verifiedPackages)
                    .Callback<IAccessCondition, StringCache>((_, sc) =>
                    {
                        sc.Dedupe(new string('a', 1));     // 1: hit    2: hit
                        sc.Dedupe(new string('b', 1));     // 1: hit    2: hit
                        sc.Dedupe(new string('c', 1));     // 1: miss   2: hit
                        sc.Dedupe(new string('c', 1));     // 1: miss   2: hit
                    });

                await _target.TryLoadAsync(_token);
                await _target.TryLoadAsync(_token);

                _telemetryService.Verify(
                    x => x.TrackAuxiliaryFilesStringCache(3, 3, 7, 4),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackAuxiliaryFilesStringCache(4, 4, 9, 8),
                    Times.Once);
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
                Assert.Same(_downloadData.Metadata, value.Metadata.Downloads);
                Assert.Same(_verifiedPackages.Metadata, value.Metadata.VerifiedPackages);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IDownloadDataClient> _downloadDataClient;
            protected readonly Mock<IVerifiedPackagesDataClient> _verifiedPackagesDataClient;
            protected readonly Mock<IPopularityTransferDataClient> _popularityTransferDataClient;
            protected readonly Mock<IAzureSearchTelemetryService> _telemetryService;
            protected readonly RecordingLogger<AuxiliaryDataCache> _logger;
            protected readonly CancellationToken _token;
            protected readonly AuxiliaryFileResult<DownloadData> _downloadData;
            protected readonly AuxiliaryFileResult<HashSet<string>> _verifiedPackages;
            protected readonly AuxiliaryFileResult<PopularityTransferData> _popularityTransfers;
            protected readonly AuxiliaryDataCache _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _downloadDataClient = new Mock<IDownloadDataClient>();
                _verifiedPackagesDataClient = new Mock<IVerifiedPackagesDataClient>();
                _popularityTransferDataClient = new Mock<IPopularityTransferDataClient>();
                _telemetryService = new Mock<IAzureSearchTelemetryService>();
                _logger = output.GetLogger<AuxiliaryDataCache>();

                _token = CancellationToken.None;
                _downloadData = Data.GetAuxiliaryFileResult(new DownloadData(), "downloads-etag");
                _verifiedPackages = Data.GetAuxiliaryFileResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase), "verified-packages-etag");
                _popularityTransfers = Data.GetAuxiliaryFileResult(new PopularityTransferData(), "popularity-transfer-etag");

                _downloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => _downloadData);
                _verifiedPackagesDataClient
                    .Setup(x => x.ReadLatestAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => _verifiedPackages);
                _popularityTransferDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => _popularityTransfers);

                _target = new AuxiliaryDataCache(
                    _downloadDataClient.Object,
                    _verifiedPackagesDataClient.Object,
                    _popularityTransferDataClient.Object,
                    _telemetryService.Object,
                    _logger);
            }

            public void VerifyReadWithNoETag()
            {
                VerifyReadWithETags(null, null);
            }

            public void VerifyReadWithETag()
            {
                VerifyReadWithETags(_downloadData.Metadata.ETag, _verifiedPackages.Metadata.ETag);
            }

            private void VerifyReadWithETags(string downloadETag, string verifiedPackagesETag)
            {
                _downloadDataClient.Verify(
                    x => x.ReadLatestIndexedAsync(
                        It.Is<IAccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == downloadETag),
                        It.IsAny<StringCache>()),
                    Times.Once);
                _downloadDataClient.Verify(
                    x => x.ReadLatestIndexedAsync(
                        It.IsAny<IAccessCondition>(),
                        It.IsAny<StringCache>()), Times.Once);
                _verifiedPackagesDataClient.Verify(
                    x => x.ReadLatestAsync(
                        It.Is<IAccessCondition>(a => a.IfMatchETag == null && a.IfNoneMatchETag == verifiedPackagesETag),
                        It.IsAny<StringCache>()),
                    Times.Once);
                _verifiedPackagesDataClient.Verify(
                    x => x.ReadLatestAsync(
                        It.IsAny<IAccessCondition>(),
                        It.IsAny<StringCache>()),
                    Times.Once);
            }

            public void VerifyNoRead()
            {
                _downloadDataClient.Verify(
                    x => x.ReadLatestIndexedAsync(
                        It.IsAny<IAccessCondition>(),
                        It.IsAny<StringCache>()), Times.Never);
                _verifiedPackagesDataClient.Verify(
                    x => x.ReadLatestAsync(
                        It.IsAny<IAccessCondition>(),
                        It.IsAny<StringCache>()), Times.Never);
            }
        }
    }
}
