﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NuGetGallery;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class ReposIndexerFacts
    {
        private static ReposIndexer CreateIndexer(
            WritableRepositoryInformation searchResult,
            IReadOnlyList<GitFileInfo> repoFiles,
            Mock<ITelemetryService> mockTelemetry,
            Action<string> onDisposeHandler,
            Func<ICheckedOutFile, IReadOnlyList<string>> configFileParser = null,
            bool shouldTimeOut = false)
        {
            var mockConfig = new Mock<IOptionsSnapshot<GitHubIndexerConfiguration>>();
            var config = new GitHubIndexerConfiguration();
            if (shouldTimeOut)
            {
                config.RepoIndexingTimeout = TimeSpan.Zero;
            }

            mockConfig
                .SetupGet(x => x.Value)
                .Returns(config);

            var mockSearcher = new Mock<IGitRepoSearcher>();
            mockSearcher
                .Setup(x => x.GetPopularRepositories())
                .Returns(Task.FromResult(new List<WritableRepositoryInformation>() { searchResult } as IReadOnlyList<WritableRepositoryInformation> ?? new List<WritableRepositoryInformation>()));

            var mockRepoCache = new Mock<IRepositoriesCache>();
            RepositoryInformation mockVal;
            mockRepoCache
                .Setup(x => x.TryGetCachedVersion(It.IsAny<WritableRepositoryInformation>(), out mockVal))
                .Callback(() => {
                    if (shouldTimeOut)
                    {
                        // A minute should be long enough to cancel the task.
                        // If the task isn't canceled, the test runtime will only be minorly affected.
                        Thread.Sleep(60 * 1000);
                    }
                })
                .Returns(false); // Simulate no cache
            mockRepoCache
                .Setup(x => x.Persist(It.IsAny<RepositoryInformation>()));

            var mockConfigFileParser = new Mock<IConfigFileParser>();
            mockConfigFileParser
                .Setup(x => x.Parse(It.IsAny<ICheckedOutFile>()))
                .Returns(configFileParser ?? ((ICheckedOutFile file) => new List<string>()));

            var mockFetchedRepo = new Mock<IFetchedRepo>();
            mockFetchedRepo
                .Setup(x => x.GetFileInfos())
                .Returns(repoFiles);
            mockFetchedRepo
                .Setup(x => x.CheckoutFiles(It.IsAny<IReadOnlyCollection<string>>()))
                .Returns((IReadOnlyCollection<string> paths) =>
                    paths.Select(x => new CheckedOutFile(filePath: x, repoId: searchResult.Id) as ICheckedOutFile).ToList());

            var mockRepoFetcher = new Mock<IRepoFetcher>();
            mockRepoFetcher
                .Setup(x => x.FetchRepo(It.IsAny<WritableRepositoryInformation>()))
                .Returns(mockFetchedRepo.Object);

            var mockBlobClient = new Mock<ICloudBlobClient>();
            var mockBlobContainer = new Mock<ICloudBlobContainer>();
            var mockBlob = new Mock<ISimpleCloudBlob>();

            mockBlobClient
                .Setup(x => x.GetContainerReference(It.IsAny<string>()))
                .Returns(() => mockBlobContainer.Object);
            mockBlobContainer
                .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                .Returns(() => mockBlob.Object);
            mockBlob
                .Setup(x => x.ETag)
                .Returns("\"some-etag\"");
            mockBlob
            .Setup(x => x.Properties)
            .Returns(Mock.Of<ICloudBlobProperties>());
            mockBlob
                .Setup(x => x.OpenWriteAsync(It.IsAny<IAccessCondition>(), It.IsAny<string>()))
                .ReturnsAsync(() => new RecordingStream(bytes =>
                    {
                        onDisposeHandler?.Invoke(Encoding.UTF8.GetString(bytes));
                    }));

            return new ReposIndexer(
                mockSearcher.Object,
                Mock.Of<ILogger<ReposIndexer>>(),
                mockRepoCache.Object,
                mockConfigFileParser.Object,
                mockRepoFetcher.Object,
                mockBlobClient.Object,
                mockConfig.Object,
                mockTelemetry.Object);
        }

        public class TheRunMethod
        {
            [Fact]
            public async Task TracksNotCompleted()
            {
                // Arrange
                var exception = new NotSupportedException("Look ma, I'm an exception!");

                var options = new Mock<IOptionsSnapshot<GitHubIndexerConfiguration>>();
                options
                    .Setup(o => o.Value)
                    .Returns(new GitHubIndexerConfiguration());

                var searcher = new Mock<IGitRepoSearcher>();
                searcher
                    .Setup(s => s.GetPopularRepositories())
                    .Throws(exception);

                var telemetry = new Mock<ITelemetryService>();

                var target = new ReposIndexer(
                    searcher.Object,
                    Mock.Of<ILogger<ReposIndexer>>(),
                    Mock.Of<IRepositoriesCache>(),
                    Mock.Of<IConfigFileParser>(),
                    Mock.Of<IRepoFetcher>(),
                    Mock.Of<ICloudBlobClient>(),
                    options.Object,
                    telemetry.Object);

                // Act
                var result = await Assert.ThrowsAsync<NotSupportedException>(
                    () => target.RunAsync());

                // Assert
                Assert.Equal("Look ma, I'm an exception!", result.Message);

                telemetry.Verify(
                    t => t.TrackRunDuration(It.IsAny<TimeSpan>(), /* completed: */ false),
                    Times.Once);
            }

            [Fact]
            public async Task TestNoDependenciesInFiles()
            {
                var repo = new WritableRepositoryInformation("owner/test", url: "", stars: 100, description: "", mainBranch: "master");
                var configFileNames = new string[] { "packages.config", "someProjFile.csproj", "someProjFile.props", "someProjFile.targets" };
                var repoFiles = new List<GitFileInfo>()
                {
                    new GitFileInfo("file1.txt", 1),
                    new GitFileInfo("file2.txt", 1),
                    new GitFileInfo(configFileNames[0], 1),
                    new GitFileInfo(configFileNames[1], 1),
                    new GitFileInfo(configFileNames[2], 1),
                    new GitFileInfo(configFileNames[3], 1)
                };

                var telemetry = new Mock<ITelemetryService>();
                var indexMetric = new Mock<IDisposable>();
                var listMetric = new Mock<IDisposable>();
                var checkoutMetric = new Mock<IDisposable>();
                var uploadMetric = new Mock<IDisposable>();
                telemetry
                    .Setup(t => t.TrackIndexRepositoryDuration("owner/test"))
                    .Returns(indexMetric.Object);
                telemetry
                    .Setup(t => t.TrackListFilesDuration("owner/test"))
                    .Returns(listMetric.Object);
                telemetry
                    .Setup(t => t.TrackCheckOutFilesDuration("owner/test"))
                    .Returns(checkoutMetric.Object);
                telemetry
                    .Setup(t => t.TrackUploadGitHubUsageBlobDuration())
                    .Returns(uploadMetric.Object);

                var indexer = CreateIndexer(
                    repo,
                    repoFiles,
                    telemetry,
                    // This should not be called since there is no dependencies
                    onDisposeHandler: (string serializedValue) => Assert.True(false));
                await indexer.RunAsync();

                var result = repo.ToRepositoryInformation();
                Assert.Empty(result.Dependencies);

                telemetry.Verify(t => t.TrackIndexRepositoryDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackListFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackCheckOutFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackUploadGitHubUsageBlobDuration(), Times.Never);
                telemetry.Verify(
                    t => t.TrackRunDuration(It.IsAny<TimeSpan>(), /* completed: */ true),
                    Times.Once);

                indexMetric.Verify(m => m.Dispose(), Times.Once);
                listMetric.Verify(m => m.Dispose(), Times.Once);
                checkoutMetric.Verify(m => m.Dispose(), Times.Once);
                uploadMetric.Verify(m => m.Dispose(), Times.Never);

                telemetry.Verify(t => t.TrackEmptyGitHubUsageBlob(), Times.Once);
            }

            [Fact]
            public async Task TestTimeoutCancelsProcessing()
            {
                var repo = new WritableRepositoryInformation("owner/test", url: "", stars: 100, description: "", mainBranch: "master");
                var configFileNames = new string[] { "packages.config", "someProjFile.csproj", "someProjFile.props", "someProjFile.targets" };
                var repoFiles = new List<GitFileInfo>()
                {
                    new GitFileInfo("file1.txt", 1),
                    new GitFileInfo("file2.txt", 1),
                    new GitFileInfo(configFileNames[0], 1),
                    new GitFileInfo(configFileNames[1], 1),
                    new GitFileInfo(configFileNames[2], 1),
                    new GitFileInfo(configFileNames[3], 1)
                };

                var telemetry = new Mock<ITelemetryService>();
                var indexer = CreateIndexer(
                    repo,
                    repoFiles,
                    telemetry,
                    // This should not be called because the process will be cancelled before it writes.
                    onDisposeHandler: (string serializedValue) => Assert.True(false),
                    shouldTimeOut: true);

                await Assert.ThrowsAsync<OperationCanceledException>(() => indexer.RunAsync());
            }

            [Fact]
            public async Task TestWithDependenciesInFiles()
            {
                var repo = new WritableRepositoryInformation("owner/test", url: "", stars: 100, description: "", mainBranch: "master");
                var configFileNames = new string[] { "packages.config", "someProjFile.csproj", "someProjFile.props", "someProjFile.targets" };
                var repoDependencies = new string[] { "dependency1", "dependency2", "dependency3", "dependency4" };
                var repoFiles = new List<GitFileInfo>()
                {
                    new GitFileInfo("file1.txt", 1),
                    new GitFileInfo("file2.txt", 1),
                    new GitFileInfo(configFileNames[0], 1),
                    new GitFileInfo(configFileNames[1], 1),
                    new GitFileInfo(configFileNames[2], 1),
                    new GitFileInfo(configFileNames[3], 1)
                };

                var telemetry = new Mock<ITelemetryService>();
                var indexMetric = new Mock<IDisposable>();
                var listMetric = new Mock<IDisposable>();
                var checkoutMetric = new Mock<IDisposable>();
                var uploadMetric = new Mock<IDisposable>();
                telemetry
                    .Setup(t => t.TrackIndexRepositoryDuration("owner/test"))
                    .Returns(indexMetric.Object);
                telemetry
                    .Setup(t => t.TrackListFilesDuration("owner/test"))
                    .Returns(listMetric.Object);
                telemetry
                    .Setup(t => t.TrackCheckOutFilesDuration("owner/test"))
                    .Returns(checkoutMetric.Object);
                telemetry
                    .Setup(t => t.TrackUploadGitHubUsageBlobDuration())
                    .Returns(uploadMetric.Object);

                var writeToBlobCalled = false;
                var indexer = CreateIndexer(repo, repoFiles, telemetry, configFileParser: (ICheckedOutFile file) =>
                    {
                        // Make sure that the Indexer filters out the non-config files
                        Assert.True(Array.Exists(configFileNames, x => string.Equals(x, file.Path)));
                        return repoDependencies;
                    },
                    onDisposeHandler: (string serializedText) =>
                    {
                        writeToBlobCalled = true;
                        Assert.Equal(JsonConvert.SerializeObject(new RepositoryInformation[] { repo.ToRepositoryInformation() }), serializedText);
                    });
                await indexer.RunAsync();

                var result = repo.ToRepositoryInformation();

                // Make sure the dependencies got read correctly
                Assert.Equal(repoDependencies.Length, result.Dependencies.Count);
                Assert.Equal(repoDependencies, result.Dependencies);

                // Make sure the repo information didn't get changed in the process
                Assert.Equal(repo.Id, result.Id);
                Assert.Equal(repo.Stars, result.Stars);
                Assert.Equal(repo.Url, result.Url);

                // Make sure the blob has been written
                Assert.True(writeToBlobCalled);

                // Verify telemetry
                telemetry.Verify(t => t.TrackIndexRepositoryDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackListFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackCheckOutFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackUploadGitHubUsageBlobDuration(), Times.Once);
                telemetry.Verify(
                    t => t.TrackRunDuration(It.IsAny<TimeSpan>(), /* completed: */ true),
                    Times.Once);

                indexMetric.Verify(m => m.Dispose(), Times.Once);
                listMetric.Verify(m => m.Dispose(), Times.Once);
                checkoutMetric.Verify(m => m.Dispose(), Times.Once);
                uploadMetric.Verify(m => m.Dispose(), Times.Once);

                telemetry.Verify(t => t.TrackEmptyGitHubUsageBlob(), Times.Never);
            }

            [Fact]
            public async Task OversizedBlobs()
            {
                var repo = new WritableRepositoryInformation("owner/test", url: "", stars: 100, description: "", mainBranch: "master");
                var configFileNames = new string[] { "packages.config", "someProjFile.csproj", "someProjFile.props", "someProjFile.targets" };
                var repoDependencies = new string[] { "dependency1", "dependency2", "dependency3", "dependency4" };
                var repoFiles = new List<GitFileInfo>()
                {
                    new GitFileInfo("file1.txt", 1),
                    new GitFileInfo("file2.txt", 1),
                    new GitFileInfo(configFileNames[0], ReposIndexer.MaxBlobSizeBytes + 1),
                    new GitFileInfo(configFileNames[1], 1),
                    new GitFileInfo(configFileNames[2], 1),
                    new GitFileInfo(configFileNames[3], 1)
                };

                var telemetry = new Mock<ITelemetryService>();
                var indexMetric = new Mock<IDisposable>();
                var listMetric = new Mock<IDisposable>();
                var checkoutMetric = new Mock<IDisposable>();
                var uploadMetric = new Mock<IDisposable>();
                telemetry
                    .Setup(t => t.TrackIndexRepositoryDuration("owner/test"))
                    .Returns(indexMetric.Object);
                telemetry
                    .Setup(t => t.TrackListFilesDuration("owner/test"))
                    .Returns(listMetric.Object);
                telemetry
                    .Setup(t => t.TrackCheckOutFilesDuration("owner/test"))
                    .Returns(checkoutMetric.Object);
                telemetry
                    .Setup(t => t.TrackUploadGitHubUsageBlobDuration())
                    .Returns(uploadMetric.Object);

                var writeToBlobCalled = false;
                var indexer = CreateIndexer(repo, repoFiles, telemetry, configFileParser: (ICheckedOutFile file) =>
                    {
                        var idx = Array.FindIndex(configFileNames, x => string.Equals(x, file.Path));

                        // Make sure that the Indexer filters out the non-config files
                        Assert.True(idx != -1);
                        Assert.True(
                            repoFiles[repoFiles.FindIndex(f => string.Equals(f.Path, file.Path))]
                                .BlobSize < ReposIndexer.MaxBlobSizeBytes);
                        return repoDependencies;
                    },
                    onDisposeHandler: (string serializedText) =>
                    {
                        writeToBlobCalled = true;
                        Assert.Equal(JsonConvert.SerializeObject(new RepositoryInformation[] { repo.ToRepositoryInformation() }), serializedText);
                    });
                await indexer.RunAsync();

                var result = repo.ToRepositoryInformation();

                // Make sure the dependencies got read correctly
                Assert.Equal(repoDependencies.Length, result.Dependencies.Count);
                Assert.Equal(repoDependencies, result.Dependencies);

                // Make sure the repo information didn't get changed in the process
                Assert.Equal(repo.Id, result.Id);
                Assert.Equal(repo.Stars, result.Stars);
                Assert.Equal(repo.Url, result.Url);

                // Make sure the blob has been written
                Assert.True(writeToBlobCalled);

                // Verify telemetry
                telemetry.Verify(t => t.TrackIndexRepositoryDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackListFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackCheckOutFilesDuration("owner/test"), Times.Once);
                telemetry.Verify(t => t.TrackUploadGitHubUsageBlobDuration(), Times.Once);
                telemetry.Verify(
                    t => t.TrackRunDuration(It.IsAny<TimeSpan>(), /* completed: */ true),
                    Times.Once);

                indexMetric.Verify(m => m.Dispose(), Times.Once);
                listMetric.Verify(m => m.Dispose(), Times.Once);
                checkoutMetric.Verify(m => m.Dispose(), Times.Once);
                uploadMetric.Verify(m => m.Dispose(), Times.Once);

                telemetry.Verify(t => t.TrackEmptyGitHubUsageBlob(), Times.Never);
            }
        }
        private class RecordingStream : MemoryStream
        {
            private readonly object _lock = new object();
            private Action<byte[]> _onDispose;

            public RecordingStream(Action<byte[]> onDispose)
            {
                _onDispose = onDispose;
            }

            protected override void Dispose(bool disposing)
            {
                lock (_lock)
                {
                    if (_onDispose != null)
                    {
                        _onDispose(ToArray());
                        _onDispose = null;
                    }
                }

                base.Dispose(disposing);
            }
        }
    }
}
