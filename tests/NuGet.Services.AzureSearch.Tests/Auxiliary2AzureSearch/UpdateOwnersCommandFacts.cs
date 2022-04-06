// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class UpdateOwnersCommandFacts
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
                Pusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Never);
                Pusher.Verify(x => x.TryFinishAsync(), Times.Never);
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
                    x => x.CompareOwners(
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>(),
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>()),
                    Times.Once);
                OwnerSetComparer.Verify(
                    x => x.CompareOwners(StorageResult.Result, DatabaseResult),
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
                Pusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Exactly(3));
                Pusher.Verify(x => x.TryFinishAsync(), Times.Once);
            }

            [Fact]
            public async Task RetriesFailedPushes()
            {
                Changes["NuGet.Core"] = new string[0];
                Changes["NuGet.Versioning"] = new string[0];
                Changes["EntityFramework"] = new string[0];
                Pusher
                    .SetupSequence(x => x.TryPushFullBatchesAsync())
                    // Attempt #1
                    .ReturnsAsync(new BatchPusherResult(new[] { "EntityFramework" }))
                    .ReturnsAsync(new BatchPusherResult(new[] { "NuGet.Core" }))
                    .ReturnsAsync(new BatchPusherResult())
                    // Attempt #2
                    .ReturnsAsync(new BatchPusherResult())
                    .ReturnsAsync(new BatchPusherResult())
                    .ReturnsAsync(new BatchPusherResult());

                await Target.ExecuteAsync();

                Pusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(6));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Core", It.IsAny<IndexActions>()),
                    Times.Exactly(2));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Exactly(2));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("EntityFramework", It.IsAny<IndexActions>()),
                    Times.Exactly(2));
                Pusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Exactly(6));
                Pusher.Verify(x => x.TryFinishAsync(), Times.Exactly(2));
            }

            [Fact]
            public async Task FailsAfterRetries()
            {
                Changes["NuGet.Core"] = new string[0];
                Changes["NuGet.Versioning"] = new string[0];
                Changes["EntityFramework"] = new string[0];
                Pusher
                    .Setup(x => x.TryPushFullBatchesAsync())
                    .ReturnsAsync(new BatchPusherResult(new[] { "EntityFramework" }));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.ExecuteAsync());

                Assert.Equal("The index operations for the following package IDs failed due to version list concurrency: EntityFramework", ex.Message);
                Pusher.Verify(
                    x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()),
                    Times.Exactly(9));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Core", It.IsAny<IndexActions>()),
                    Times.Exactly(3));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("NuGet.Versioning", It.IsAny<IndexActions>()),
                    Times.Exactly(3));
                Pusher.Verify(
                    x => x.EnqueueIndexActions("EntityFramework", It.IsAny<IndexActions>()),
                    Times.Exactly(3));
                Pusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Exactly(9));
                Pusher.Verify(x => x.TryFinishAsync(), Times.Exactly(3));
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
                Pusher.Verify(x => x.TryPushFullBatchesAsync(), Times.Exactly(5));
                Pusher.Verify(x => x.TryFinishAsync(), Times.Exactly(32));
            }

            [Fact]
            public async Task UpdatesBlobStorageAfterIndexing()
            {
                var actions = new List<string>();
                Pusher
                    .Setup(x => x.TryFinishAsync())
                    .ReturnsAsync(new BatchPusherResult())
                    .Callback(() => actions.Add(nameof(IBatchPusher.TryFinishAsync)));
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
                    new[] { nameof(IBatchPusher.TryFinishAsync), nameof(IOwnerDataClient.UploadChangeHistoryAsync), nameof(IOwnerDataClient.ReplaceLatestIndexedAsync) },
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
                DatabaseOwnerFetcher = new Mock<IDatabaseAuxiliaryDataFetcher>();
                OwnerDataClient = new Mock<IOwnerDataClient>();
                OwnerSetComparer = new Mock<IDataSetComparer>();
                SearchDocumentBuilder = new Mock<ISearchDocumentBuilder>();
                SearchIndexActionBuilder = new Mock<ISearchIndexActionBuilder>();
                Pusher = new Mock<IBatchPusher>();
                Options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<UpdateOwnersCommand>();

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
                    new List<IndexDocumentsAction<KeyedDocument>> { IndexDocumentsAction.Merge(new KeyedDocument()) },
                    new List<IndexDocumentsAction<KeyedDocument>> { IndexDocumentsAction.Merge(new KeyedDocument()) },
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        new Mock<IAccessCondition>().Object));

                Pusher.SetReturnsDefault(Task.FromResult(new BatchPusherResult()));
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
                    .Setup(x => x.CompareOwners(
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>(),
                        It.IsAny<SortedDictionary<string, SortedSet<string>>>()))
                    .Returns(() => Changes);
                SearchIndexActionBuilder
                    .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Func<SearchFilters, KeyedDocument>>()))
                    .ReturnsAsync(() => IndexActions);

                Target = new UpdateOwnersCommand(
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

            public Mock<IDatabaseAuxiliaryDataFetcher> DatabaseOwnerFetcher { get; }
            public Mock<IOwnerDataClient> OwnerDataClient { get; }
            public Mock<IDataSetComparer> OwnerSetComparer { get; }
            public Mock<ISearchDocumentBuilder> SearchDocumentBuilder { get; }
            public Mock<ISearchIndexActionBuilder> SearchIndexActionBuilder { get; }
            public Mock<IBatchPusher> Pusher { get; }
            public Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> Options { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<UpdateOwnersCommand> Logger { get; }
            public AzureSearchJobConfiguration Configuration { get; }
            public SortedDictionary<string, SortedSet<string>> DatabaseResult { get; }
            public ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>> StorageResult { get; }
            public SortedDictionary<string, string[]> Changes { get; }
            public IndexActions IndexActions { get; }
            public UpdateOwnersCommand Target { get; }
        }
    }
}
