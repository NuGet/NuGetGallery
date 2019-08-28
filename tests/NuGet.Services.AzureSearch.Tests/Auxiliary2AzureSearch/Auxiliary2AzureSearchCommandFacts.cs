// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class Auxiliary2AzureSearchCommandFacts
    {
        public class ExecuteAsync : Facts
        {
            public ExecuteAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task PushesAddedVerifiedPackage()
            {
                NewVerifiedPackagesData.Add("NuGet.Versioning");

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(
                        NewVerifiedPackagesData,
                        It.Is<IAccessCondition>(a => a.IfMatchETag == OldVerifiedPackagesResult.Metadata.ETag)),
                    Times.Once);
            }

            [Fact]
            public async Task PushesRemovedVerifiedPackage()
            {
                OldVerifiedPackagesData.Add("NuGet.Versioning");

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(
                        NewVerifiedPackagesData,
                        It.Is<IAccessCondition>(a => a.IfMatchETag == OldVerifiedPackagesResult.Metadata.ETag)),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotPushUnchangedVerifiedPackages()
            {
                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.NoOp);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(It.IsAny<HashSet<string>>(), It.IsAny<IAccessCondition>()),
                    Times.Never);
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
                BatchPusher.Verify(x => x.FinishAsync(), Times.Never);
                BatchPusher.Verify(x => x.PushFullBatchesAsync(), Times.Never);
                DownloadDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(It.IsAny<DownloadData>(), It.IsAny<IAccessCondition>()),
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
                    new List<IndexAction<KeyedDocument>>(
                        Enumerable
                            .Range(0, documentsPerId)
                            .Select(x => IndexAction.Merge(new KeyedDocument()))),
                    new List<IndexAction<KeyedDocument>>(),
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
                BatchPusher.Verify(x => x.FinishAsync(), Times.Exactly(expectedPushes));
                BatchPusher.Verify(x => x.PushFullBatchesAsync(), Times.Never);
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
                BatchPusher.Verify(x => x.FinishAsync(), Times.AtLeastOnce);
                BatchPusher.Verify(x => x.PushFullBatchesAsync(), Times.Never);
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
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                AuxiliaryFileClient = new Mock<IAuxiliaryFileClient>();
                DownloadDataClient = new Mock<IDownloadDataClient>();
                VerifiedPackagesDataClient = new Mock<IVerifiedPackagesDataClient>();
                DownloadSetComparer = new Mock<IDownloadSetComparer>();
                SearchDocumentBuilder = new Mock<ISearchDocumentBuilder>();
                IndexActionBuilder = new Mock<ISearchIndexActionBuilder>();
                BatchPusher = new Mock<IBatchPusher>();
                SystemTime = new Mock<ISystemTime>();
                Options = new Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<Auxiliary2AzureSearchCommand>();

                Config = new Auxiliary2AzureSearchConfiguration
                {
                    AzureSearchBatchSize = 10,
                    MaxConcurrentBatches = 1,
                    MaxConcurrentVersionListWriters = 1,
                    MinPushPeriod = TimeSpan.FromSeconds(5),
                };
                Options.Setup(x => x.Value).Returns(() => Config);

                OldDownloadData = new DownloadData();
                OldDownloadResult = Data.GetAuxiliaryFileResult(OldDownloadData, "download-data-etag");
                DownloadDataClient
                    .Setup(x => x.ReadLatestIndexedAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => OldDownloadResult);
                NewDownloadData = new DownloadData();
                AuxiliaryFileClient.Setup(x => x.LoadDownloadDataAsync()).ReturnsAsync(() => NewDownloadData);

                OldVerifiedPackagesData = new HashSet<string>();
                OldVerifiedPackagesResult = Data.GetAuxiliaryFileResult(OldVerifiedPackagesData, "verified-packages-etag");
                VerifiedPackagesDataClient
                    .Setup(x => x.ReadLatestAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => OldVerifiedPackagesResult);
                NewVerifiedPackagesData = new HashSet<string>();
                AuxiliaryFileClient.Setup(x => x.LoadVerifiedPackagesAsync()).ReturnsAsync(() => NewVerifiedPackagesData);

                Changes = new SortedDictionary<string, long>();
                DownloadSetComparer
                    .Setup(x => x.Compare(It.IsAny<DownloadData>(), It.IsAny<DownloadData>()))
                    .Returns(() => Changes);

                IndexActions = new IndexActions(
                    new List<IndexAction<KeyedDocument>> { IndexAction.Merge(new KeyedDocument()) },
                    new List<IndexAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        new Mock<IAccessCondition>().Object));
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
                    .Setup(x => x.FinishAsync())
                    .Returns(Task.Delay(TimeSpan.FromMilliseconds(1)))
                    .Callback(() =>
                    {
                        FinishedBatches.Add(CurrentBatch.ToList());
                        CurrentBatch = new ConcurrentBag<IndexActions>();
                    });

                Target = new Auxiliary2AzureSearchCommand(
                    AuxiliaryFileClient.Object,
                    DownloadDataClient.Object,
                    VerifiedPackagesDataClient.Object,
                    DownloadSetComparer.Object,
                    SearchDocumentBuilder.Object,
                    IndexActionBuilder.Object,
                    () => BatchPusher.Object,
                    SystemTime.Object,
                    Options.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IAuxiliaryFileClient> AuxiliaryFileClient { get; }
            public Mock<IDownloadDataClient> DownloadDataClient { get; }
            public Mock<IVerifiedPackagesDataClient> VerifiedPackagesDataClient { get; }
            public Mock<IDownloadSetComparer> DownloadSetComparer { get; }
            public Mock<ISearchDocumentBuilder> SearchDocumentBuilder { get; }
            public Mock<ISearchIndexActionBuilder> IndexActionBuilder { get; }
            public Mock<IBatchPusher> BatchPusher { get; }
            public Mock<ISystemTime> SystemTime { get; }
            public Mock<IOptionsSnapshot<Auxiliary2AzureSearchConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<Auxiliary2AzureSearchCommand> Logger { get; }
            public Auxiliary2AzureSearchConfiguration Config { get; }
            public DownloadData OldDownloadData { get; }
            public AuxiliaryFileResult<DownloadData> OldDownloadResult { get; }
            public DownloadData NewDownloadData { get; }
            public SortedDictionary<string, long> Changes { get; }
            public Auxiliary2AzureSearchCommand Target { get; }
            public IndexActions IndexActions { get; set; }
            public ConcurrentBag<string> ProcessedIds { get; }
            public ConcurrentBag<string> PushedIds { get; }
            public ConcurrentBag<IndexActions> CurrentBatch { get; set; }
            public ConcurrentBag<List<IndexActions>> FinishedBatches { get; }
            public HashSet<string> OldVerifiedPackagesData { get; }
            public AuxiliaryFileResult<HashSet<string>> OldVerifiedPackagesResult { get; }
            public HashSet<string> NewVerifiedPackagesData { get; }

            public void VerifyCompletedTelemetry(JobOutcome outcome)
            {
                TelemetryService.Verify(
                    x => x.TrackAuxiliary2AzureSearchCompleted(It.IsAny<JobOutcome>(), It.IsAny<TimeSpan>()),
                    Times.Once);
                TelemetryService.Verify(
                    x => x.TrackAuxiliary2AzureSearchCompleted(outcome, It.IsAny<TimeSpan>()),
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
                var processedIds = ProcessedIds.OrderBy(x => x).ToArray();
                var pushedIds = PushedIds.OrderBy(x => x).ToArray();

                Assert.Equal(changeCount, changedIds.Length);
                Assert.Equal(changedIds, processedIds);
                Assert.Equal(changedIds, pushedIds);
            }
        }
    }
}
