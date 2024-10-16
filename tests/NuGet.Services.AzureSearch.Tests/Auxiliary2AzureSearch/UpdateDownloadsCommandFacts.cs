// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class UpdateDownloadsCommandFacts
    {
        public class ExecuteAsync : Facts
        {
            public ExecuteAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task PushesNothingWhenThereAreNoChanges()
            {
                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.NoOp);
                VerifyAllIdsAreProcessed(changeCount: 0);
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        It.IsAny<string>(),
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Never);
                BatchPusher.Verify(x => x.TryFinishAsync(), Times.Never);
                BatchPusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
                DownloadsV1JsonClient.Verify(
                    x => x.ReadAsync(),
                    Times.Once);
                DownloadDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(It.IsAny<DownloadData>(), It.IsAny<IAccessCondition>()),
                    Times.Never);
                PopularityTransferDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(It.IsAny<PopularityTransferData>(), It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(1, 7, 4,  1)] // 1, 2, ... 7 + 4 = 11 is greater than 10 so 7 is the batch size.
            [InlineData(2, 8, 7,  1)] // 2, 4, ... 8 + 4 = 12 is greater than 10 so 8 is the batch size.
            [InlineData(3, 9, 10, 0)] // 3, 6,     9 + 4 = 13 is greater than 10 so 9 is the batch size.
            [InlineData(4, 8, 15, 0)] // 4,        8 + 4 = 12 is greater than 10 so 8 is the batch size.
            public async Task RespectsAzureSearchBatchSize(int documentsPerId, int batchSize, int fullPushes, int partialPushes)
            {
                var changeCount = 30;
                var expectedPushes = fullPushes + partialPushes;
                Config.AzureSearchBatchSize = 10;

                IndexActions = new IndexActions(
                    new List<IndexDocumentsAction<KeyedDocument>>(
                        Enumerable
                            .Range(0, documentsPerId)
                            .Select(x => IndexDocumentsAction.Merge(new KeyedDocument()))),
                    new List<IndexDocumentsAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        new Mock<IAccessCondition>().Object));      

                AddChanges(changeCount);

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifyAllIdsAreProcessed(changeCount);
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        It.IsAny<string>(),
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Exactly(changeCount));
                BatchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(changeCount));
                BatchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(expectedPushes));
                BatchPusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
                SystemTime.Verify(x => x.Delay(It.IsAny<TimeSpan>()), Times.Exactly(expectedPushes - 1));
                DownloadDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(
                        NewDownloadData,
                        It.Is<IAccessCondition>(a => a.IfMatchETag == OldDownloadResult.Metadata.ETag)),
                    Times.Once);

                Assert.Equal(
                    fullPushes,
                    FinishedBatches.Count(b => b.Sum(ia => ia.Search.Count) == batchSize));
                Assert.Equal(
                    partialPushes,
                    FinishedBatches.Count(b => b.Sum(ia => ia.Search.Count) != batchSize));
                Assert.Empty(CurrentBatch);
            }

            [Fact]
            public async Task CanProcessInParallel()
            {
                var changeCount = 1000;
                Config.AzureSearchBatchSize = 5;
                Config.MaxConcurrentBatches = 4;
                Config.MaxConcurrentVersionListWriters = 8;
                AddChanges(changeCount);

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifyAllIdsAreProcessed(changeCount);
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        It.IsAny<string>(),
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Exactly(changeCount));
                BatchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(changeCount));
                BatchPusher.Verify(x => x.TryFinishAsync(), Times.AtLeastOnce);
                BatchPusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
            }

            [Fact]
            public async Task RetriesFailedPackageIds()
            {
                Config.MaxConcurrentVersionListWriters = 1;
                Changes["PackageA"] = 1;
                Changes["PackageB"] = 2;
                BatchPusher
                    .SetupSequence(x => x.TryFinishAsync())
                    .ReturnsAsync(new BatchPusherResult(new[] { "PackageB" }))
                    .ReturnsAsync(new BatchPusherResult());

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifyAllIdsAreProcessed(new[] { "PackageA", "PackageB", "PackageB" });
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        "PackageA",
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Once);
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        "PackageB",
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Exactly(2));
                BatchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(3));
                BatchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(2));
                BatchPusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
            }

            [Fact]
            public async Task EventuallyFailsIfBatchPusherNeverSucceeds()
            {
                Config.MaxConcurrentVersionListWriters = 1;
                Changes["PackageA"] = 1;
                Changes["PackageB"] = 2;
                BatchPusher
                    .Setup(x => x.TryFinishAsync())
                    .ReturnsAsync(new BatchPusherResult(new[] { "PackageB" }));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ExecuteAsync());

                Assert.Equal("The index operations for the following package IDs failed due to version list concurrency: PackageB", ex.Message);
                VerifyCompletedTelemetry(JobOutcome.Failure);
                VerifyAllIdsAreProcessed(new[] { "PackageA", "PackageB", "PackageB", "PackageB" });
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        "PackageA",
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Once);
                IndexActionBuilder.Verify(
                    x => x.UpdateAsync(
                        "PackageB",
                        It.IsAny<Func<SearchFilters, KeyedDocument>>()),
                    Times.Exactly(3));
                BatchPusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(4));
                BatchPusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));
                BatchPusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
            }

            [Fact]
            public async Task FailureIsRecordedInTelemetry()
            {
                var expected = new InvalidOperationException("Something bad!");
                DownloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ThrowsAsync(expected);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ExecuteAsync());

                VerifyCompletedTelemetry(JobOutcome.Failure);
                Assert.Same(expected, actual);
            }

            [Theory]
            [InlineData(nameof(NewDownloadData))]
            [InlineData(nameof(OldDownloadData))]
            public async Task RejectsInvalidDataAndNormalizesVersions(string propertyName)
            {
                var downloadData = (DownloadData)GetType().GetProperty(propertyName).GetValue(this);
                downloadData.SetDownloadCount("ValidId", "1.0.0-ValidVersion", 3);
                downloadData.SetDownloadCount("ValidId", "1.0.0.a-invalidversion", 5);
                downloadData.SetDownloadCount("ValidId", "1.0.0.0-NonNormalized", 7);
                downloadData.SetDownloadCount("Invalid--Id", "1.0.0-validversion", 11);
                downloadData.SetDownloadCount("Invalid--Id", "1.0.0.a-invalidversion", 13);

                await Target.ExecuteAsync();

                Assert.Equal(new[] { "ValidId" }, downloadData.Keys.ToArray());
                Assert.Equal(new[] { "1.0.0-NonNormalized", "1.0.0-ValidVersion" }, downloadData["ValidId"].Keys.OrderBy(x => x).ToArray());
                Assert.Equal(10, downloadData.GetDownloadCount("ValidId"));
                Assert.Contains("There were 1 invalid IDs, 2 invalid versions, and 1 non-normalized IDs.", Logger.Messages);
            }

            [Fact]
            public async Task AppliesTransferChanges()
            {
                var downloadChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                DownloadSetComparer
                    .Setup(c => c.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns<DownloadData, DownloadData>((oldData, newData) =>
                    {
                        return downloadChanges;
                    });

                TransferChanges["Package1"] = 100;
                TransferChanges["Package2"] = 200;

                NewTransfers.AddTransfer("Package1", "Package2");

                await Target.ExecuteAsync();

                PopularityTransferDataClient
                    .Verify(
                        c => c.ReadLatestIndexedAsync(
                            It.Is<IAccessCondition>(x => x.IfMatchETag == null && x.IfNoneMatchETag == null),
                            It.IsAny<StringCache>()),
                        Times.Once);
                DatabaseFetcher
                    .Verify(
                        d => d.GetPopularityTransfersAsync(),
                        Times.Once);

                DownloadTransferrer
                    .Verify(
                        x => x.UpdateDownloadTransfers(
                            NewDownloadData,
                            downloadChanges,
                            OldTransfers,
                            NewTransfers),
                        Times.Once);

                // Documents should be updated.
                SearchDocumentBuilder
                    .Verify(
                        b => b.UpdateDownloadCount("Package1", SearchFilters.IncludePrereleaseAndSemVer2, 100),
                        Times.Once);
                SearchDocumentBuilder
                    .Verify(
                        b => b.UpdateDownloadCount("Package2", SearchFilters.IncludePrereleaseAndSemVer2, 200),
                        Times.Once);

                // Downloads auxiliary file should not include transfer changes.
                DownloadDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<DownloadData>(d => d.Count == 0),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);

                // Popularity transfers auxiliary file should have new data.
                PopularityTransferDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<PopularityTransferData>(d =>
                            d.Count == 1 &&
                            d["Package1"].Count == 1 &&
                            d["Package1"].Contains("Package2")),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task ConfigDisablesPopularityTransfers()
            {
                var downloadChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Package1", 5 }
                };

                DownloadSetComparer
                    .Setup(c => c.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns<DownloadData, DownloadData>((oldData, newData) =>
                    {
                        return downloadChanges;
                    });

                OldTransfers.AddTransfer("Package1", "Package2");
                NewTransfers.AddTransfer("Package1", "Package2");

                Config.EnablePopularityTransfers = false;

                await Target.ExecuteAsync();

                PopularityTransferDataClient
                    .Verify(
                        c => c.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()),
                        Times.Once);
                DatabaseFetcher
                    .Verify(
                        d => d.GetPopularityTransfersAsync(),
                        Times.Never);

                // The popularity transfers should not be given to the download transferrer.
                DownloadTransferrer
                    .Verify(
                        x => x.UpdateDownloadTransfers(
                            NewDownloadData,
                            downloadChanges,
                            OldTransfers,
                            It.Is<PopularityTransferData>(d => d.Count == 0)),
                        Times.Once);

                // Popularity transfers auxiliary file should be empty.
                PopularityTransferDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<PopularityTransferData>(d => d.Count == 0),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task FlagDisablesPopularityTransfers()
            {
                var downloadChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Package1", 5 }
                };

                DownloadSetComparer
                    .Setup(c => c.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns<DownloadData, DownloadData>((oldData, newData) =>
                    {
                        return downloadChanges;
                    });

                OldTransfers.AddTransfer("Package1", "Package2");
                NewTransfers.AddTransfer("Package1", "Package2");

                FeatureFlags
                    .Setup(x => x.IsPopularityTransferEnabled())
                    .Returns(false);

                await Target.ExecuteAsync();

                PopularityTransferDataClient
                    .Verify(
                        c => c.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()),
                        Times.Once);
                DatabaseFetcher
                    .Verify(
                        d => d.GetPopularityTransfersAsync(),
                        Times.Never);

                // The popularity transfers should not be given to the download transferrer.
                DownloadTransferrer
                    .Verify(
                        x => x.UpdateDownloadTransfers(
                            NewDownloadData,
                            downloadChanges,
                            OldTransfers,
                            It.Is<PopularityTransferData>(d => d.Count == 0)),
                        Times.Once);

                // Popularity transfers auxiliary file should be empty.
                PopularityTransferDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<PopularityTransferData>(d => d.Count == 0),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task TransferChangesOverrideDownloadChanges()
            {
                DownloadSetComparer
                    .Setup(c => c.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns<DownloadData, DownloadData>((oldData, newData) =>
                    {
                        return new SortedDictionary<string, long>(
                            newData.ToDictionary(d => d.Key, d => d.Value.Total),
                            StringComparer.OrdinalIgnoreCase);
                    });

                NewDownloadData.SetDownloadCount("A", "1.0.0", 12);
                NewDownloadData.SetDownloadCount("A", "2.0.0", 34);

                NewDownloadData.SetDownloadCount("B", "3.0.0", 5);
                NewDownloadData.SetDownloadCount("B", "4.0.0", 4);

                NewDownloadData.SetDownloadCount("C", "5.0.0", 2);
                NewDownloadData.SetDownloadCount("C", "6.0.0", 3);

                TransferChanges["A"] = 55;
                TransferChanges["b"] = 66;

                NewTransfers.AddTransfer("A", "b");

                await Target.ExecuteAsync();

                // Documents should have new data with transfer changes.
                SearchDocumentBuilder
                    .Verify(
                        b => b.UpdateDownloadCount("A", SearchFilters.IncludePrereleaseAndSemVer2, 55),
                        Times.Once);
                SearchDocumentBuilder
                    .Verify(
                        b => b.UpdateDownloadCount("B", SearchFilters.IncludePrereleaseAndSemVer2, 66),
                        Times.Once);
                SearchDocumentBuilder
                    .Verify(
                        b => b.UpdateDownloadCount("C", SearchFilters.IncludePrereleaseAndSemVer2, 5),
                        Times.Once);

                // Downloads auxiliary file should not reflect transfer changes.
                DownloadDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<DownloadData>(d =>
                            d["A"].Total == 46 &&
                            d["A"]["1.0.0"] == 12 &&
                            d["A"]["2.0.0"] == 34 &&

                            d["B"].Total == 9 &&
                            d["B"]["3.0.0"] == 5 &&
                            d["B"]["4.0.0"] == 4 &&

                            d["C"].Total == 5 &&
                            d["C"]["5.0.0"] == 2 &&
                            d["C"]["6.0.0"] == 3),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);

                // Popularity transfers auxiliary file should have new data.
                PopularityTransferDataClient.Verify(
                    c => c.ReplaceLatestIndexedAsync(
                        It.Is<PopularityTransferData>(d =>
                            d.Count == 1 &&
                            d["A"].Count == 1 &&
                            d["A"].Contains("b")),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                DownloadsV1JsonClient = new Mock<IDownloadsV1JsonClient>();
                DatabaseFetcher = new Mock<IDatabaseAuxiliaryDataFetcher>();
                DownloadDataClient = new Mock<IDownloadDataClient>();
                DownloadSetComparer = new Mock<IDownloadSetComparer>();
                DownloadTransferrer = new Mock<IDownloadTransferrer>();
                PopularityTransferDataClient = new Mock<IPopularityTransferDataClient>();
                SearchDocumentBuilder = new Mock<ISearchDocumentBuilder>();
                IndexActionBuilder = new Mock<ISearchIndexActionBuilder>();
                BatchPusher = new Mock<IBatchPusher>();
                SystemTime = new Mock<ISystemTime>();
                FeatureFlags = new Mock<IFeatureFlagService>();
                Options = new Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<Auxiliary2AzureSearchCommand>();

                Config = new Auxiliary2AzureSearchConfiguration
                {
                    DownloadsV1JsonUrl = "https://example/downloads.v1.json",
                    AzureSearchBatchSize = 10,
                    MaxConcurrentBatches = 1,
                    MaxConcurrentVersionListWriters = 1,
                    EnablePopularityTransfers = true,
                    MinPushPeriod = TimeSpan.FromSeconds(5),
                };
                Options.Setup(x => x.Value).Returns(() => Config);

                OldDownloadData = new DownloadData();
                OldDownloadResult = Data.GetAuxiliaryFileResult(OldDownloadData, "download-data-etag");
                DownloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => OldDownloadResult);
                NewDownloadData = new DownloadData();
                DownloadsV1JsonClient.Setup(x => x.ReadAsync()).ReturnsAsync(() => NewDownloadData);

                Changes = new SortedDictionary<string, long>();
                DownloadSetComparer
                    .Setup(x => x.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns(() => Changes);

                OldTransfers = new PopularityTransferData();
                OldTransferResult = new AuxiliaryFileResult<PopularityTransferData>(
                    modified: true,
                    data: OldTransfers,
                    metadata: new AuxiliaryFileMetadata(
                        DateTimeOffset.UtcNow,
                        TimeSpan.Zero,
                        fileSize: 0,
                        etag: "etag"));
                PopularityTransferDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(OldTransferResult);

                NewTransfers = new PopularityTransferData();
                DatabaseFetcher
                    .Setup(x => x.GetPopularityTransfersAsync())
                    .ReturnsAsync(NewTransfers);

                TransferChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                DownloadTransferrer
                    .Setup(x => x.UpdateDownloadTransfers(
                        It.IsAny<DownloadData>(),
                        It.IsAny<SortedDictionary<string, long>>(),
                        It.IsAny<PopularityTransferData>(),
                        It.IsAny<PopularityTransferData>()))
                    .Returns(TransferChanges);

                IndexActions = new IndexActions(
                    new List<IndexDocumentsAction<KeyedDocument>> { IndexDocumentsAction.Merge(new KeyedDocument()) },
                    new List<IndexDocumentsAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        Mock.Of<IAccessCondition>()));
                ProcessedIds = new ConcurrentBag<string>();
                IndexActionBuilder
                    .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Func<SearchFilters, KeyedDocument>>()))
                    .ReturnsAsync(() => IndexActions)
                    .Callback<string, Func<SearchFilters, KeyedDocument>>((id, b) =>
                    {
                        ProcessedIds.Add(id);
                        b(SearchFilters.IncludePrereleaseAndSemVer2);
                    });

                // When pushing, delay for a little bit of time so the stopwatch has some measurable duration.
                PushedIds = new ConcurrentBag<string>();
                CurrentBatch = new ConcurrentBag<IndexActions>();
                FinishedBatches = new ConcurrentBag<List<IndexActions>>();
                BatchPusher
                    .Setup(x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()))
                    .Callback<string, IndexActions>((id, indexActions) =>
                    {
                        CurrentBatch.Add(indexActions);
                        PushedIds.Add(id);
                    });
                BatchPusher
                    .Setup(x => x.TryFinishAsync())
                    .Returns(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1));
                        return new BatchPusherResult();
                    })
                    .Callback(() =>
                    {
                        FinishedBatches.Add(CurrentBatch.ToList());
                        CurrentBatch = new ConcurrentBag<IndexActions>();
                    });

                FeatureFlags.Setup(x => x.IsPopularityTransferEnabled()).Returns(true);

                Target = new UpdateDownloadsCommand(
                    DownloadsV1JsonClient.Object,
                    DatabaseFetcher.Object,
                    DownloadDataClient.Object,
                    DownloadSetComparer.Object,
                    DownloadTransferrer.Object,
                    PopularityTransferDataClient.Object,
                    SearchDocumentBuilder.Object,
                    IndexActionBuilder.Object,
                    () => BatchPusher.Object,
                    SystemTime.Object,
                    FeatureFlags.Object,
                    Options.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IDownloadsV1JsonClient> DownloadsV1JsonClient { get; }
            public Mock<IDatabaseAuxiliaryDataFetcher> DatabaseFetcher { get; }
            public Mock<IDownloadDataClient> DownloadDataClient { get; }
            public Mock<IDownloadSetComparer> DownloadSetComparer { get; }
            public Mock<IDownloadTransferrer> DownloadTransferrer { get; }
            public Mock<IPopularityTransferDataClient> PopularityTransferDataClient { get; }
            public Mock<ISearchDocumentBuilder> SearchDocumentBuilder { get; }
            public Mock<ISearchIndexActionBuilder> IndexActionBuilder { get; }
            public Mock<IBatchPusher> BatchPusher { get; }
            public Mock<ISystemTime> SystemTime { get; }
            public Mock<IFeatureFlagService> FeatureFlags { get; }
            public Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<Auxiliary2AzureSearchCommand> Logger { get; }
            public Auxiliary2AzureSearchConfiguration Config { get; }
            public DownloadData OldDownloadData { get; }
            public AuxiliaryFileResult<DownloadData> OldDownloadResult { get; }
            public DownloadData NewDownloadData { get; }
            public PopularityTransferData OldTransfers { get; }
            public AuxiliaryFileResult<PopularityTransferData> OldTransferResult { get; }
            public PopularityTransferData NewTransfers { get; }
            public SortedDictionary<string, long> Changes { get; }
            public SortedDictionary<string, long> TransferChanges { get; }
            public UpdateDownloadsCommand Target { get; }
            public IndexActions IndexActions { get; set; }
            public ConcurrentBag<string> ProcessedIds { get; }
            public ConcurrentBag<string> PushedIds { get; }
            public ConcurrentBag<IndexActions> CurrentBatch { get; set; }
            public ConcurrentBag<List<IndexActions>> FinishedBatches { get; }

            public void VerifyCompletedTelemetry(JobOutcome outcome)
            {
                TelemetryService.Verify(
                    x => x.TrackUpdateDownloadsCompleted(It.IsAny<JobOutcome>(), It.IsAny<TimeSpan>()),
                    Times.Once);
                TelemetryService.Verify(
                    x => x.TrackUpdateDownloadsCompleted(outcome, It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            public void AddChanges(int changeCount)
            {
                for (var i = 1; i <= changeCount; i++)
                {
                    Changes[$"Package{i}"] = i;
                }
            }

            public void VerifyAllIdsAreProcessed(int changeCount)
            {
                var changedIds = Changes.Keys.OrderBy(x => x).ToArray();
                Assert.Equal(changeCount, changedIds.Length);
                VerifyAllIdsAreProcessed(changedIds);
            }

            public void VerifyAllIdsAreProcessed(string[] changedIds)
            {
                var processedIds = ProcessedIds.OrderBy(x => x).ToArray();
                var pushedIds = PushedIds.OrderBy(x => x).ToArray();

                Assert.Equal(changedIds, processedIds);
                Assert.Equal(changedIds, pushedIds);
            }
        }
    }
}
