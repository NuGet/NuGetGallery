// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class Owners2AzureSearchCommandFacts
    {
        public class ExecuteAsync : Facts
        {
            public ExecuteAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotPushWhenThereAreNoChanges()
            {
                await Target.ExecuteAsync();

                Pusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Never);
                Pusher.Verify(x => x.PushFullBatchesAsync(), Times.Never);
                Pusher.Verify(x => x.FinishAsync(), Times.Never);
                OwnerDataClient.Verify(x => x.UploadChangeHistoryAsync(It.IsAny<IReadOnlyList<string>>()), Times.Never);
                OwnerDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task ComparesInTheRightOrder()
            {
                await Target.ExecuteAsync();

                OwnerSetComparer.Verify(
                    x => x.Compare(
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>(),
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>()),
                    Times.Once);
                OwnerSetComparer.Verify(
                    x => x.Compare(StorageResult.Result, DatabaseResult),
                    Times.Once);
            }

            [Fact]
            public async Task PushesAllChangesWithSingleWorker()
            {
                Changes["NuGet.Core"] = new string[0];
                Changes["NuGet.Versioning"] = new string[0];
                Changes["EntityFramework"] = new string[0];

                await Target.ExecuteAsync();

                Pusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(3));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Core", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("EntityFramework", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(x => x.PushFullBatchesAsync(), Times.Exactly(3));
                Pusher.Verify(x => x.FinishAsync(), Times.Once);
            }

            [Fact]
            public async Task PushesAllChangesWithMultipleWorkers()
            {
                Configuration.MaxConcurrentBatches = 32;

                Changes["NuGet.Core"] = new string[0];
                Changes["NuGet.Versioning"] = new string[0];
                Changes["EntityFramework"] = new string[0];
                Changes["Microsoft.Extensions.Logging"] = new string[0];
                Changes["Microsoft.Extensions.DependencyInjection"] = new string[0];

                await Target.ExecuteAsync();

                Pusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(5));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Core", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("EntityFramework", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("Microsoft.Extensions.Logging", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(
                    x => x.EnqueueIndexActions("Microsoft.Extensions.DependencyInjection", It.IsAny<IndexActions>()),
                    Times.Once);
                Pusher.Verify(x => x.PushFullBatchesAsync(), Times.Exactly(5));
                Pusher.Verify(x => x.FinishAsync(), Times.Exactly(32));
            }

            [Fact]
            public async Task UpdatesBlobStorageAfterIndexing()
            {
                var actions = new List<string>();
                Pusher
                    .Setup(x => x.FinishAsync())
                    .Returns(Task.CompletedTask)
                    .Callback(() => actions.Add(nameof(IBatchPusher.FinishAsync)));
                OwnerDataClient
                    .Setup(x => x.UploadChangeHistoryAsync(It.IsAny<IReadOnlyList<string>>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => actions.Add(nameof(IOwnerDataClient.UploadChangeHistoryAsync)));
                OwnerDataClient
                    .Setup(x => x.ReplaceLatestIndexedAsync(It.IsAny<SortedDictionary<string, SortedSet<string>>>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => actions.Add(nameof(IOwnerDataClient.ReplaceLatestIndexedAsync)));

                Changes["NuGet.Core"] = new string[0];

                await Target.ExecuteAsync();

                Assert.Equal(
                    new[] { nameof(IBatchPusher.FinishAsync), nameof(IOwnerDataClient.UploadChangeHistoryAsync), nameof(IOwnerDataClient.ReplaceLatestIndexedAsync) },
                    actions.ToArray());
            }

            [Fact]
            public async Task UpdatesBlobStorage()
            {
                IReadOnlyList<string> changeHistory = null;
                OwnerDataClient
                    .Setup(x => x.UploadChangeHistoryAsync(It.IsAny<IReadOnlyList<string>>()))
                    .Returns(Task.CompletedTask)
                    .Callback<IReadOnlyList<string>>(x => changeHistory = x);

                Changes["NuGet.Versioning"] = new string[0];
                Changes["NuGet.Core"] = new string[0];

                await Target.ExecuteAsync();

                Assert.Equal(new[] { "NuGet.Core", "NuGet.Versioning" }, changeHistory.ToArray());
                OwnerDataClient.Verify(
                    x => x.ReplaceLatestIndexedAsync(DatabaseResult, StorageResult.AccessCondition),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                DatabaseOwnerFetcher = new Mock<IDatabaseOwnerFetcher>();
                OwnerDataClient = new Mock<IOwnerDataClient>();
                OwnerSetComparer = new Mock<IOwnerSetComparer>();
                SearchDocumentBuilder = new Mock<ISearchDocumentBuilder>();
                SearchIndexActionBuilder = new Mock<ISearchIndexActionBuilder>();
                Pusher = new Mock<IBatchPusher>();
                Options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<Owners2AzureSearchCommand>();

                Configuration = new AzureSearchJobConfiguration
                {
                    MaxConcurrentBatches = 1,
                };
                DatabaseResult = new SortedDictionary<string, SortedSet<string>>();
                StorageResult = new ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>>(
                    new SortedDictionary<string, SortedSet<string>>(),
                    new Mock<IAccessCondition>().Object);
                Changes = new SortedDictionary<string, string[]>();
                IndexActions = new IndexActions(
                    new List<IndexAction<KeyedDocument>> { IndexAction.Merge(new KeyedDocument()) },
                    new List<IndexAction<KeyedDocument>> { IndexAction.Merge(new KeyedDocument()) },
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        new Mock<IAccessCondition>().Object));

                Options
                    .Setup(x => x.Value)
                    .Returns(() => Configuration);
                DatabaseOwnerFetcher
                    .Setup(x => x.GetPackageIdToOwnersAsync())
                    .ReturnsAsync(() => DatabaseResult);
                OwnerDataClient
                    .Setup(x => x.ReadLatestIndexedAsync())
                    .ReturnsAsync(() => StorageResult);
                OwnerSetComparer
                    .Setup(x => x.Compare(
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>(),
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>()))
                    .Returns(() => Changes);
                SearchIndexActionBuilder
                    .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Func<SearchFilters, KeyedDocument>>()))
                    .ReturnsAsync(() => IndexActions);

                Target = new Owners2AzureSearchCommand(
                    DatabaseOwnerFetcher.Object,
                    OwnerDataClient.Object,
                    OwnerSetComparer.Object,
                    SearchDocumentBuilder.Object,
                    SearchIndexActionBuilder.Object,
                    () => Pusher.Object,
                    Options.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IDatabaseOwnerFetcher> DatabaseOwnerFetcher { get; }
            public Mock<IOwnerDataClient> OwnerDataClient { get; }
            public Mock<IOwnerSetComparer> OwnerSetComparer { get; }
            public Mock<ISearchDocumentBuilder> SearchDocumentBuilder { get; }
            public Mock<ISearchIndexActionBuilder> SearchIndexActionBuilder { get; }
            public Mock<IBatchPusher> Pusher { get; }
            public Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<Owners2AzureSearchCommand> Logger { get; }
            public AzureSearchJobConfiguration Configuration { get; }
            public SortedDictionary<string, SortedSet<string>> DatabaseResult { get; }
            public ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>> StorageResult { get; }
            public SortedDictionary<string, string[]> Changes { get; }
            public IndexActions IndexActions { get; }
            public Owners2AzureSearchCommand Target { get; }
        }
    }
}
