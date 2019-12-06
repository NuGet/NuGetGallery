// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public partial class HiveMergerFacts
    {
        public class TheMergeAsyncMethod : Facts
        {
            public TheMergeAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(Versions))]
            public async Task AddSingleFirstVersion(string version)
            {
                var indexInfo = IndexInfo.New();
                var sortedCatalog = MakeSortedCatalog(Details(version));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);

                var pageInfo = Assert.Single(indexInfo.Items);
                var page = await pageInfo.GetPageAsync();
                var leafInfo = Assert.Single(await pageInfo.GetLeafInfosAsync());
                var leafItem = Assert.Single(page.Items);
                Assert.Same(leafItem, leafInfo.LeafItem);
                Assert.Equal(version, leafItem.CatalogEntry.Version);

                Assert.Same(pageInfo, Assert.Single(result.ModifiedPages));
                Assert.Same(leafInfo, Assert.Single(result.ModifiedLeaves));
                Assert.Empty(result.DeletedLeaves);
            }

            [Theory]
            [MemberData(nameof(Versions))]
            public async Task UpdateSingleVersion(string version)
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", version, "4.0.0", "5.0.0");
                var sortedCatalog = MakeSortedCatalog(Details(version));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.False(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                var pageA = await indexInfo.Items[0].GetPageAsync();
                var pageB = await indexInfo.Items[1].GetPageAsync();
                Assert.Equal(3, pageA.Items.Count);
                Assert.Equal("1.0.0", pageA.Items[0].CatalogEntry.Version);
                Assert.Equal("2.0.0", pageA.Items[1].CatalogEntry.Version);
                Assert.Equal(version, pageA.Items[2].CatalogEntry.Version);
                Assert.Equal(2, pageB.Items.Count);
                Assert.Equal("4.0.0", pageB.Items[0].CatalogEntry.Version);
                Assert.Equal("5.0.0", pageB.Items[1].CatalogEntry.Version);

                var pageInfo = Assert.Single(result.ModifiedPages);
                Assert.Same(indexInfo.Items[0], pageInfo);
                var leafInfo = (await pageInfo.GetLeafInfosAsync())[2];
                Assert.Same(leafInfo, Assert.Single(result.ModifiedLeaves));
                Assert.Empty(result.DeletedLeaves);
            }

            [Theory]
            [MemberData(nameof(Versions))]
            public async Task RemoveSingleExisting(string version)
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", version, "4.0.0", "5.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete(version));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.True(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                var pageA = await indexInfo.Items[0].GetPageAsync();
                var pageB = await indexInfo.Items[1].GetPageAsync();
                Assert.Equal(3, pageA.Items.Count);
                Assert.Equal("1.0.0", pageA.Items[0].CatalogEntry.Version);
                Assert.Equal("2.0.0", pageA.Items[1].CatalogEntry.Version);
                Assert.Equal("4.0.0", pageA.Items[2].CatalogEntry.Version);
                Assert.Equal("5.0.0", Assert.Single(pageB.Items).CatalogEntry.Version);

                Assert.Contains(indexInfo.Items[0], result.ModifiedPages);
                Assert.Contains(indexInfo.Items[1], result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Equal(version, Assert.Single(result.DeletedLeaves).LeafItem.CatalogEntry.Version);
            }

            [Fact]
            public async Task DeleteAgainstEmpty()
            {
                var indexInfo = IndexInfo.New();
                var sortedCatalog = MakeSortedCatalog(Delete("1.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.Empty(indexInfo.Items);

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task DeleteLast()
            {
                var indexInfo = MakeIndexInfo("1.0.0");
                var leafInfo = (await indexInfo.Items.First().GetLeafInfosAsync()).First();
                var sortedCatalog = MakeSortedCatalog(Delete("1.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.Empty(indexInfo.Items);

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Same(leafInfo, Assert.Single(result.DeletedLeaves));
            }

            [Fact]
            public async Task DeleteLastThenAddOne()
            {
                var indexInfo = MakeIndexInfo("1.0.0");
                var leafInfoA = (await indexInfo.Items.First().GetLeafInfosAsync()).First();
                var sortedCatalog = MakeSortedCatalog(Delete("1.0.0"), Details("2.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);

                var pageInfo = Assert.Single(indexInfo.Items);
                var page = await pageInfo.GetPageAsync();
                var leafInfoB = Assert.Single(await pageInfo.GetLeafInfosAsync());
                var leafItem = Assert.Single(page.Items);
                Assert.Same(leafItem, leafInfoB.LeafItem);
                Assert.NotSame(leafInfoA, leafInfoB);
                Assert.Equal("2.0.0", leafItem.CatalogEntry.Version);

                Assert.Same(pageInfo, Assert.Single(result.ModifiedPages));
                Assert.Same(leafInfoB, Assert.Single(result.ModifiedLeaves));
                Assert.Same(leafInfoA, Assert.Single(result.DeletedLeaves));
            }

            [Fact]
            public async Task DeleteNonExistentThenAddOne()
            {
                var indexInfo = MakeIndexInfo("1.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("2.0.0"), Details("3.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);

                var pageInfo = Assert.Single(indexInfo.Items);
                var leafInfos = await pageInfo.GetLeafInfosAsync();
                Assert.Equal(2, leafInfos.Count);
                Assert.Equal("1.0.0", leafInfos[0].LeafItem.CatalogEntry.Version);
                Assert.Equal("3.0.0", leafInfos[1].LeafItem.CatalogEntry.Version);

                Assert.Same(pageInfo, Assert.Single(result.ModifiedPages));
                Assert.Same(leafInfos[1], Assert.Single(result.ModifiedLeaves));
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task RemoveLastVersionFromPage()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("4.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.False(indexInfo.Items[0].IsPageFetched);

                var pageInfo = Assert.Single(indexInfo.Items);
                var page = await pageInfo.GetPageAsync();
                Assert.Equal(3, page.Items.Count);
                Assert.Equal("1.0.0", page.Items[0].CatalogEntry.Version);
                Assert.Equal("2.0.0", page.Items[1].CatalogEntry.Version);
                Assert.Equal("3.0.0", page.Items[2].CatalogEntry.Version);

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Equal("4.0.0", Assert.Single(result.DeletedLeaves).LeafItem.CatalogEntry.Version);
            }

            [Fact]
            public async Task RemoveVersionFromPageCausingLastPageToBeRemoved()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("3.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);

                var pageInfo = Assert.Single(indexInfo.Items);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "4.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Same(pageInfo, Assert.Single(result.ModifiedPages));
                Assert.Empty(result.ModifiedLeaves);
                Assert.Equal("3.0.0", Assert.Single(result.DeletedLeaves).LeafItem.CatalogEntry.Version);
            }

            [Fact]
            public async Task InsertInMiddlePage()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0", "6.0.0", "7.0.0", "8.0.0");
                var sortedCatalog = MakeSortedCatalog(Details("5.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.False(indexInfo.Items[0].IsPageFetched);
                Assert.True(indexInfo.Items[1].IsPageFetched);
                Assert.True(indexInfo.Items[2].IsPageFetched);

                Assert.Equal(3, indexInfo.Items.Count);
                Assert.Equal(
                    new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0", "5.0.0", "6.0.0", "7.0.0", "8.0.0" },
                    await GetVersionArrayAsync(indexInfo));

                Assert.Equal(2, result.ModifiedPages.Count);
                Assert.DoesNotContain(indexInfo.Items[0], result.ModifiedPages);
                Assert.Contains(indexInfo.Items[1], result.ModifiedPages);
                Assert.Contains(indexInfo.Items[2], result.ModifiedPages);
                Assert.Equal("5.0.0", Assert.Single(result.ModifiedLeaves).LeafItem.CatalogEntry.Version);
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task InsertInLastPage()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0", "5.0.0", "6.0.0", "8.0.0");
                var sortedCatalog = MakeSortedCatalog(Details("7.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.False(indexInfo.Items[0].IsPageFetched);
                Assert.False(indexInfo.Items[1].IsPageFetched);
                Assert.True(indexInfo.Items[2].IsPageFetched);

                Assert.Equal(3, indexInfo.Items.Count);
                Assert.Equal(
                    new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0", "5.0.0", "6.0.0", "7.0.0", "8.0.0" },
                    await GetVersionArrayAsync(indexInfo));

                Assert.Single(result.ModifiedPages);
                Assert.Contains(indexInfo.Items[2], result.ModifiedPages);
                Assert.Equal("7.0.0", Assert.Single(result.ModifiedLeaves).LeafItem.CatalogEntry.Version);
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task InsertLowestCreatingNewPage()
            {
                var indexInfo = MakeIndexInfo("2.0.0", "3.0.0", "4.0.0");
                var sortedCatalog = MakeSortedCatalog(Details("1.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.True(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Equal(2, result.ModifiedPages.Count);
                Assert.Contains(indexInfo.Items[0], result.ModifiedPages);
                Assert.Contains(indexInfo.Items[1], result.ModifiedPages);
                Assert.Equal("1.0.0", Assert.Single(result.ModifiedLeaves).LeafItem.CatalogEntry.Version);
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task AppendLatestVersionCreatingNewPage()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0");
                var sortedCatalog = MakeSortedCatalog(Details("4.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.False(indexInfo.Items[0].IsPageFetched);
                Assert.True(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Equal(indexInfo.Items[1], Assert.Single(result.ModifiedPages));
                Assert.Equal("4.0.0", Assert.Single(result.ModifiedLeaves).LeafItem.CatalogEntry.Version);
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task InterleaveVersions()
            {
                var indexInfo = MakeIndexInfo("2.0.0", "4.0.0", "6.0.0", "8.0.0", "10.0.0");
                var sortedCatalog = MakeSortedCatalog(
                    Details("1.0.0"),
                    Details("3.0.0"),
                    Details("5.0.0"),
                    Details("7.0.0"),
                    Details("9.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.True(indexInfo.Items[1].IsPageFetched);
                Assert.True(indexInfo.Items[2].IsPageFetched);
                Assert.True(indexInfo.Items[3].IsPageFetched);

                Assert.Equal(4, indexInfo.Items.Count);
                Assert.Equal(
                    new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0", "5.0.0", "6.0.0", "7.0.0", "8.0.0", "9.0.0", "10.0.0" },
                    await GetVersionArrayAsync(indexInfo));

                Assert.Equal(4, result.ModifiedPages.Count);
                Assert.Equal(new[] { "1.0.0", "3.0.0", "5.0.0", "7.0.0", "9.0.0" }, GetVersionArray(result.ModifiedLeaves));
                Assert.Empty(result.DeletedLeaves);
            }

            [Fact]
            public async Task DeleteAllVersions()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("1.0.0"), Delete("2.0.0"), Delete("3.0.0"), Delete("4.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.Empty(indexInfo.Items);
                Assert.Empty(await GetVersionArrayAsync(indexInfo));

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" }, GetVersionArray(result.DeletedLeaves));
            }

            [Fact]
            public async Task AddManyVersions()
            {
                var indexInfo = IndexInfo.New();
                var sortedCatalog = MakeSortedCatalog(Details("1.0.0"), Details("2.0.0"), Details("3.0.0"), Details("4.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Equal(2, result.ModifiedPages.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" }, GetVersionArray(result.ModifiedLeaves));
                Assert.Empty(result.DeletedLeaves);
            }

            [Theory]
            [InlineData("2.0.0", "1.0.0")]
            [InlineData("4.0.0", "1.0.0")]
            [InlineData("6.0.0", "1.0.0")]
            [InlineData("2.0.0", "3.0.0")]
            [InlineData("4.0.0", "3.0.0")]
            [InlineData("6.0.0", "3.0.0")]
            [InlineData("2.0.0", "5.0.0")]
            [InlineData("4.0.0", "5.0.0")]
            [InlineData("6.0.0", "5.0.0")]
            [InlineData("2.0.0", "7.0.0")]
            [InlineData("4.0.0", "7.0.0")]
            [InlineData("6.0.0", "7.0.0")]
            public async Task AddAndRemoveFromNonLastPage(string deleted, string added)
            {
                var existing = new[] { "2.0.0", "4.0.0", "6.0.0", "8.0.0" };
                var expected = existing
                    .Except(new[] { deleted })
                    .Concat(new[] { added })
                    .OrderBy(x => NuGetVersion.Parse(x))
                    .ToArray();
                var actions = new[] { Delete(deleted), Details(added) }
                    .OrderBy(x => x.Version)
                    .ToList();
                var indexInfo = MakeIndexInfo(existing);
                var sortedCatalog = MakeSortedCatalog(actions);

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.False(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(expected, await GetVersionArrayAsync(indexInfo));

                Assert.Same(indexInfo.Items[0], Assert.Single(result.ModifiedPages));
                Assert.Equal(new[] { added }, GetVersionArray(result.ModifiedLeaves));
                Assert.Equal(new[] { deleted }, GetVersionArray(result.DeletedLeaves));
            }

            [Fact]
            public async Task RemoveMiddlePage()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "3.0.0", "4.0.0", "5.0.0", "6.0.0", "7.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("4.0.0"), Delete("5.0.0"), Delete("6.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.False(indexInfo.Items[0].IsPageFetched);
                Assert.False(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "3.0.0", "7.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Equal(new[] { "4.0.0", "5.0.0", "6.0.0" }, GetVersionArray(result.DeletedLeaves));
            }

            [Fact]
            public async Task LoadsPageToFindMiss()
            {
                var indexInfo = MakeIndexInfo("1.0.0", "2.0.0", "4.0.0", "5.0.0");
                var sortedCatalog = MakeSortedCatalog(Delete("3.0.0"));

                var result = await Target.MergeAsync(indexInfo, sortedCatalog);

                Assert.True(indexInfo.Items[0].IsPageFetched);
                Assert.False(indexInfo.Items[1].IsPageFetched);

                Assert.Equal(2, indexInfo.Items.Count);
                Assert.Equal(new[] { "1.0.0", "2.0.0", "4.0.0", "5.0.0" }, await GetVersionArrayAsync(indexInfo));

                Assert.Empty(result.ModifiedPages);
                Assert.Empty(result.ModifiedLeaves);
                Assert.Empty(result.DeletedLeaves);
            }

            public static IEnumerable<object[]> Versions => new[]
            {
                new object[] { "3.0.0" },
                new object[] { "3.1.0" },
                new object[] { "3.0.1" },
                new object[] { "3.0.0.1" },
                new object[] { "3.0.0-beta" },
                new object[] { "3.0.0-BETA" },
                new object[] { "3.0.0-beta.1" },
            };
        }
    }
}
