// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class OwnerIndexActionBuilderFacts
    {
        public class UpdateOwnersAsync : Facts
        {
            public UpdateOwnersAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task UpdatesSearchDocumentsWithVersionMatchingAllFilters()
            {
                VersionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        {
                            "1.0.0",
                            new VersionPropertiesData(listed: true, semVer2: false)
                        },
                    }),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                var indexActions = await Target.UpdateOwnersAsync(
                    Data.PackageId,
                    Data.Owners);

                Assert.Same(VersionListDataResult, indexActions.VersionListDataResult);
                Assert.Empty(indexActions.Hijack);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateOwners>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Merge, x.ActionType));

                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.Default.ToString());
                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.IncludePrerelease.ToString());
                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.IncludeSemVer2.ToString());
                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.IncludePrereleaseAndSemVer2.ToString());
            }

            [Fact]
            public async Task UpdatesSearchDocumentsWithVersionMatchingSomeFilters()
            {
                VersionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        {
                            "1.0.0-beta",
                            new VersionPropertiesData(listed: true, semVer2: false)
                        },
                    }),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                var indexActions = await Target.UpdateOwnersAsync(
                    Data.PackageId,
                    Data.Owners);

                Assert.Same(VersionListDataResult, indexActions.VersionListDataResult);
                Assert.Empty(indexActions.Hijack);

                Assert.Equal(2, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateOwners>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Merge, x.ActionType));

                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.IncludePrerelease.ToString());
                Assert.Single(indexActions.Search, x => x.Document.Key == SearchFilters.IncludePrereleaseAndSemVer2.ToString());
            }

            [Fact]
            public async Task UpdatesSearchDocumentsWithOnlyUnlistedVersions()
            {
                VersionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        {
                            "1.0.0-beta",
                            new VersionPropertiesData(listed: false, semVer2: false)
                        },
                    }),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                var indexActions = await Target.UpdateOwnersAsync(
                    Data.PackageId,
                    Data.Owners);

                Assert.Same(VersionListDataResult, indexActions.VersionListDataResult);
                Assert.Empty(indexActions.Hijack);
                Assert.Empty(indexActions.Search);
            }

            [Fact]
            public async Task UpdatesSearchDocumentsWithNoVersions()
            {
                VersionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                var indexActions = await Target.UpdateOwnersAsync(
                    Data.PackageId,
                    Data.Owners);

                Assert.Same(VersionListDataResult, indexActions.VersionListDataResult);
                Assert.Empty(indexActions.Hijack);
                Assert.Empty(indexActions.Search);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                VersionListDataClient = new Mock<IVersionListDataClient>();
                Search = new Mock<ISearchDocumentBuilder>();
                Logger = output.GetLogger<OwnerIndexActionBuilder>();

                VersionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                VersionListDataClient
                    .Setup(x => x.ReadAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => VersionListDataResult);
                Search
                    .Setup(x => x.UpdateOwners(It.IsAny<string>(), It.IsAny<SearchFilters>(), It.IsAny<string[]>()))
                    .Returns<string, SearchFilters, string[]>((_, sf, __) => new SearchDocument.UpdateOwners
                    {
                        Key = sf.ToString(),
                    });

                Target = new OwnerIndexActionBuilder(
                    VersionListDataClient.Object,
                    Search.Object,
                    Logger);
            }

            public Mock<IVersionListDataClient> VersionListDataClient { get; }
            public Mock<ISearchDocumentBuilder> Search { get; }
            public RecordingLogger<OwnerIndexActionBuilder> Logger { get; }
            public ResultAndAccessCondition<VersionListData> VersionListDataResult { get; set; }
            public OwnerIndexActionBuilder Target { get; }
        }
    }
}
