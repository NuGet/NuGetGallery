// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class DocumentFixUpEvaluatorFacts
    {
        public class TheTryFixUpAsyncMethod : Facts
        {
            public TheTryFixUpAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task NoInnerExceptionIsNotApplicable()
            {
                var ex = new InvalidOperationException();

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, ex);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task WrongInnerExceptionTypeIsNotApplicable()
            {
                var ex = new InvalidOperationException("Not good!", new ArgumentException());

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, ex);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task No404FailureIsNotApplicable()
            {
                IndexingResults.Add(new IndexingResult(statusCode: 503));

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, Exception);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task Unmatched404FailureIsNotApplicable()
            {
                IndexingResults.Add(new IndexingResult(statusCode: 404));

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, Exception);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task Hijack404FailureIsNotApplicable()
            {
                IndexingResults.Add(new IndexingResult(key: "hijack-doc", statusCode: 404));
                AllIndexActions.Add(new IdAndValue<IndexActions>(
                    "NuGet.Versioning",
                    new IndexActions(
                        search: new List<IndexAction<KeyedDocument>>(),
                        hijack: new List<IndexAction<KeyedDocument>>
                        {
                            IndexAction.Merge(new KeyedDocument { Key = "hijack-doc" }),
                        },
                        versionListDataResult: new ResultAndAccessCondition<VersionListData>(
                            new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                            Mock.Of<IAccessCondition>()))));

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, Exception);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task Search404NonMergeFailureIsNotApplicable()
            {
                IndexingResults.Add(new IndexingResult(key: "search-doc", statusCode: 404));
                AllIndexActions.Add(new IdAndValue<IndexActions>(
                    "NuGet.Versioning",
                    new IndexActions(
                        search: new List<IndexAction<KeyedDocument>>
                        {
                            IndexAction.Delete(new KeyedDocument { Key = "search-doc" }),
                        },
                        hijack: new List<IndexAction<KeyedDocument>>(),
                        versionListDataResult: new ResultAndAccessCondition<VersionListData>(
                            new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                            Mock.Of<IAccessCondition>()))));

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, Exception);

                Assert.False(result.Applicable);
            }

            [Fact]
            public async Task Search404MergeFailureIsApplicable()
            {
                ItemList.Add(new CatalogCommitItem(
                    new Uri("https://example/catalog/0.json"),
                    "commit-id-a",
                    new DateTime(2020, 3, 16, 12, 5, 0, DateTimeKind.Utc),
                    new string[0],
                    new[] { Schema.DataTypes.PackageDetails },
                    new PackageIdentity("NuGet.Frameworks", NuGetVersion.Parse("1.0.0"))));
                ItemList.Add(new CatalogCommitItem(
                    new Uri("https://example/catalog/1.json"),
                    "commit-id-a",
                    new DateTime(2020, 3, 16, 12, 5, 0, DateTimeKind.Utc),
                    new string[0],
                    new[] { Schema.DataTypes.PackageDetails },
                    new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("0.9.0-beta.1"))));

                IndexingResults.Add(new IndexingResult(key: "search-doc", statusCode: 404));
                AllIndexActions.Add(new IdAndValue<IndexActions>(
                    "NuGet.Versioning",
                    new IndexActions(
                        search: new List<IndexAction<KeyedDocument>>
                        {
                            IndexAction.Merge(new KeyedDocument { Key = "search-doc" }),
                        },
                        hijack: new List<IndexAction<KeyedDocument>>(),
                        versionListDataResult: new ResultAndAccessCondition<VersionListData>(
                            new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                            Mock.Of<IAccessCondition>()))));
                VersionListClient
                    .Setup(x => x.ReadAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>
                        {
                            { "1.0.0", new VersionPropertiesData(listed: true, semVer2: false) },
                        }),
                        Mock.Of<IAccessCondition>()));
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Url = "https://example/catalog/2.json",
                    CommitId = "commit-id",
                    CommitTimestamp = new DateTimeOffset(2020, 3, 17, 12, 5, 0, TimeSpan.Zero),
                    Type = CatalogLeafType.PackageDetails,
                };
                LeafFetcher
                    .Setup(x => x.GetLatestLeavesAsync(
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyList<IReadOnlyList<NuGetVersion>>>()))
                    .ReturnsAsync(() => new LatestCatalogLeaves(
                        new HashSet<NuGetVersion>(),
                        new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                        {
                            { NuGetVersion.Parse("1.0.0"), leaf },
                        }));

                var result = await Target.TryFixUpAsync(ItemList, AllIndexActions, Exception);

                Assert.True(result.Applicable, "The fix up should be applicable.");
                Assert.Equal(3, result.ItemList.Count);
                Assert.Empty(ItemList.Except(result.ItemList));

                var addedItem = Assert.Single(result.ItemList.Except(ItemList));
                Assert.Equal(leaf.Url, addedItem.Uri.AbsoluteUri);
                Assert.Equal(leaf.CommitId, addedItem.CommitId);
                Assert.Equal(leaf.CommitTimestamp, addedItem.CommitTimeStamp);
                Assert.Empty(addedItem.Types);
                Assert.Equal(Schema.DataTypes.PackageDetails, Assert.Single(addedItem.TypeUris));
                Assert.Equal(new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0")), addedItem.PackageIdentity);
                Assert.True(addedItem.IsPackageDetails, "The generated item should be a package details item.");
                Assert.False(addedItem.IsPackageDelete, "The generated item should not be a package delete item.");
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                VersionListClient = new Mock<IVersionListDataClient>();
                LeafFetcher = new Mock<ICatalogLeafFetcher>();
                Logger = output.GetLogger<DocumentFixUpEvaluator>();

                ItemList = new List<CatalogCommitItem>();
                AllIndexActions = new ConcurrentBag<IdAndValue<IndexActions>>();
                IndexingResults = new List<IndexingResult>();
                DocumentIndexResult = new DocumentIndexResult(IndexingResults);
                InnerException = new IndexBatchException(DocumentIndexResult);
                Exception = new InvalidOperationException("It broke.", InnerException);

                Target = new DocumentFixUpEvaluator(
                    VersionListClient.Object,
                    LeafFetcher.Object,
                    Logger);
            }

            public Mock<IVersionListDataClient> VersionListClient { get; }
            public Mock<ICatalogLeafFetcher> LeafFetcher { get; }
            public RecordingLogger<DocumentFixUpEvaluator> Logger { get; }
            public List<CatalogCommitItem> ItemList { get; }
            public ConcurrentBag<IdAndValue<IndexActions>> AllIndexActions { get; }
            public List<IndexingResult> IndexingResults { get; }
            public DocumentIndexResult DocumentIndexResult { get; }
            public IndexBatchException InnerException { get; }
            public InvalidOperationException Exception { get; }
            public DocumentFixUpEvaluator Target { get; }
        }
    }
}
