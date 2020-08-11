// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveUpdaterFacts
    {
        public class UpdateAsync : Facts
        {
            public UpdateAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task RejectsMissingPackageDetailsLeaves()
            {
                AddPackageDetails("2.0.0");
                EntryToCatalogLeaf.Clear();

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit));
                Assert.Equal("Each PackageDetails catalog commit item must have an associate catalog leaf.", ex.Message);
            }

            [Fact]
            public async Task RejectsDuplicateVersions()
            {
                AddPackageDetails("2.0.0");
                AddPackageDetails("2.0.0");

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit));
                Assert.Equal("There must be exactly on catalog commit item per version.", ex.Message);
            }

            [Fact]
            public async Task RejectsNonSemVer2ReplicaHiveWhenMainHiveIsSemVer2()
            {
                AddPackageDetails("2.0.0");
                Hive = HiveType.SemVer2;
                ReplicaHives.Add(HiveType.Legacy);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit));
                Assert.Equal("A replica hive of a SemVer 2.0.0 hive must also include SemVer 2.0.0.", ex.Message);
            }

            [Fact]
            public async Task RejectsSemVer2ReplicaHiveWhenMainHiveIsNonSemVer2()
            {
                AddPackageDetails("2.0.0");
                Hive = HiveType.Legacy;
                ReplicaHives.Add(HiveType.SemVer2);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit));
                Assert.Equal("A replica hive of a non-SemVer 2.0.0 hive must also exclude SemVer 2.0.0.", ex.Message);
            }

            [Fact]
            public async Task MergesCatalogCommitItems()
            {
                AddPackageDetails("2.0.0");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Merger.Verify(
                    x => x.MergeAsync(It.IsAny<IndexInfo>(), It.IsAny<IReadOnlyList<CatalogCommitItem>>()),
                    Times.Once);
                EntityBuilder.Verify(
                    x => x.NewLeaf(It.IsAny<RegistrationLeafItem>()),
                    Times.Once);
                EntityBuilder.Verify(
                    x => x.NewLeaf(It.Is<RegistrationLeafItem>(i => i.CatalogEntry.Version == "2.0.0")),
                    Times.Once);
                EntityBuilder.Verify(
                    x => x.NewLeaf(RegistrationIndex.Items[0].Items[1]),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationLeaf>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("2.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex),
                    Times.Once);
                Storage.Verify(
                    x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()),
                    Times.Never);
                Storage.Verify(
                    x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()),
                    Times.Never);
                Assert.Equal("2.0.0", RegistrationIndex.Items[0].Items[1].CatalogEntry.Version);
            }

            [Theory]
            [InlineData(HiveType.Legacy)]
            [InlineData(HiveType.Gzipped)]
            public async Task ExcludesSemVer2VersionsFromSemVer1Hives(HiveType hive)
            {
                Hive = hive;
                AddPackageDetails("2.0.0-beta.1");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                EntityBuilder.Verify(x => x.NewLeaf(It.IsAny<RegistrationLeafItem>()), Times.Never);
                Storage.Verify(
                    x => x.WriteLeafAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationLeaf>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Never);
                Assert.Equal(2, RegistrationIndex.Items[0].Items.Count);
                Assert.DoesNotContain("2.0.0-beta.1", RegistrationIndex.Items[0].Items.Select(x => x.CatalogEntry.Version));
            }

            [Fact]
            public async Task MergesSemVer2VersionsOnSemVer2Hives()
            {
                AddPackageDetails("2.0.0-beta.1");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                EntityBuilder.Verify(
                    x => x.NewLeaf(It.Is<RegistrationLeafItem>(i => i.CatalogEntry.Version == "2.0.0-beta.1")),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("2.0.0-beta.1"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex),
                    Times.Once);
                Assert.Equal("2.0.0-beta.1", RegistrationIndex.Items[0].Items[1].CatalogEntry.Version);
            }

            [Fact]
            public async Task DeletesEmptyIndex()
            {
                AddPackageDelete("1.0.0");
                AddPackageDelete("3.0.0");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(
                    x => x.WriteLeafAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationLeaf>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Never);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(Hive, ReplicaHives, Id), Times.Once);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Exactly(2));
                Storage.Verify(x => x.DeleteUrlAsync(Hive, ReplicaHives, "https://example/reg/nuget.versioning/1.0.0.json"), Times.Once);
                Storage.Verify(x => x.DeleteUrlAsync(Hive, ReplicaHives, "https://example/reg/nuget.versioning/3.0.0.json"), Times.Once);
            }

            [Fact]
            public async Task WritesExternalizedPage()
            {
                Config.MaxLeavesPerPage = 2;
                Config.MaxInlinedLeafItems = 2;
                AddPackageDetails("4.0.0");
                var page = RegistrationIndex.Items[0];

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Exactly(2));
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("3.0.0"), page),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), NuGetVersion.Parse("4.0.0"), It.IsAny<RegistrationPage>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Assert.Equal(2, RegistrationIndex.Items.Count);
                Assert.Null(RegistrationIndex.Items[0].Items);
                Assert.Null(RegistrationIndex.Items[1].Items);
            }

            [Fact]
            public async Task MovesPageThatIsAlreadyExternal()
            {
                Config.MaxInlinedLeafItems = 0;
                AddPackageDetails("4.0.0");
                var oldPageUrl = "https://example/reg/nuget.versioning/1.0.0/3.0.0.json";
                var newPageUrl = "https://example/reg/nuget.versioning/1.0.0/4.0.0.json";
                var pageItem = RegistrationIndex.Items[0];
                var page = new RegistrationPage
                {
                    Items = pageItem.Items,
                };
                pageItem.Url = oldPageUrl;
                pageItem.Items = null;
                Storage
                    .Setup(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()))
                    .ReturnsAsync(() => page);

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.ReadPageAsync(Hive, oldPageUrl), Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("4.0.0"), page),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.DeleteUrlAsync(Hive, ReplicaHives, oldPageUrl), Times.Once);
                Assert.Equal(newPageUrl, pageItem.Url);
            }

            [Fact]
            public async Task UpdatesNonInlinedIndex()
            {
                Config.MaxInlinedLeafItems = 0;
                Config.MaxLeavesPerPage = 2;
                AddPackageDetails("4.0.0");
                var existingPageItem = RegistrationIndex.Items[0];
                var existingPage = new RegistrationPage
                {
                    Items = existingPageItem.Items,
                };
                existingPageItem.Items = null;

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), NuGetVersion.Parse("4.0.0"), It.Is<RegistrationPage>(p => p.Items.Single().CatalogEntry.Version == "4.0.0")),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Assert.Equal(2, RegistrationIndex.Items.Count);
                Assert.Null(RegistrationIndex.Items[0].Items);
                Assert.Null(RegistrationIndex.Items[1].Items);
            }

            [Fact]
            public async Task InlinesPages()
            {
                AddPackageDetails("4.0.0");
                var oldPageUrl = "https://example/reg/nuget.versioning/1.0.0/3.0.0.json";
                var pageItem = RegistrationIndex.Items[0];
                var page = new RegistrationPage
                {
                    Items = pageItem.Items,
                };
                pageItem.Url = oldPageUrl;
                pageItem.Items = null;
                Storage
                    .Setup(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()))
                    .ReturnsAsync(() => page);

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.ReadPageAsync(Hive, oldPageUrl), Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Never);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.DeleteUrlAsync(Hive, ReplicaHives, oldPageUrl), Times.Once);
                Assert.NotNull(page.Items);
                Assert.Equal(3, page.Items.Count);
                Assert.Same(page, Assert.Single(RegistrationIndex.Items));
            }

            /// <summary>
            /// The count in the index can be out of date if the job crashed before writing the index but after
            /// updating a page. This is possible because a page URL can stay the same even if items are added. This
            /// can happen if items are added to in the middle of the last page.
            /// </summary>
            [Fact]
            public async Task UpdatesNonInlinedPageWithIncorrectCount()
            {
                Config.MaxInlinedLeafItems = 0;
                Config.MaxLeavesPerPage = 4;

                RegistrationIndex.Items[0].Count = 4;
                RegistrationIndex.Items[0].Items = null;

                var pageUrl = "https://example/reg/nuget.versioning/4.0.0/7.0.0.json";
                RegistrationIndex.Items.Add(new RegistrationPage
                {
                    Url = pageUrl,
                    Count = 2,
                    Lower = "4.0.0",
                    Upper = "7.0.0",
                });
                var page = new RegistrationPage
                {
                    Items = new List<RegistrationLeafItem>
                    {
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "4.0.0" } },
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "5.0.0" } },
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "7.0.0" } },
                    },
                };
                Storage
                    .Setup(x => x.ReadPageAsync(It.IsAny<HiveType>(), pageUrl))
                    .ReturnsAsync(() => page);

                AddPackageDetails("6.0.0");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.ReadPageAsync(Hive, pageUrl), Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("6.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), NuGetVersion.Parse("7.0.0"), page),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Assert.Equal(2, RegistrationIndex.Items.Count);
                Assert.Null(RegistrationIndex.Items[0].Items);
                Assert.Null(RegistrationIndex.Items[1].Items);
            }

            [Fact]
            public async Task SortsOutOfOrderLeafItems()
            {
                Config.MaxInlinedLeafItems = 0;
                Config.MaxLeavesPerPage = 4;

                var pageUrl = "https://example/reg/nuget.versioning/4.0.0/7.0.0.json";
                RegistrationIndex.Items[0].Url = pageUrl;
                RegistrationIndex.Items[0].Count = 2;
                RegistrationIndex.Items[0].Items = null;
                RegistrationIndex.Items[0].Lower = "4.0.0";
                RegistrationIndex.Items[0].Upper = "7.0.0";

                var page = new RegistrationPage
                {
                    Items = new List<RegistrationLeafItem>
                    {
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "5.0.0" } },
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "4.0.0" } },
                        new RegistrationLeafItem { CatalogEntry = new RegistrationCatalogEntry { Version = "7.0.0" } },
                    },
                };
                Storage
                    .Setup(x => x.ReadPageAsync(It.IsAny<HiveType>(), pageUrl))
                    .ReturnsAsync(() => page);

                AddPackageDetails("6.0.0");

                await Target.UpdateAsync(Hive, ReplicaHives, Id, Entries, EntryToCatalogLeaf, RegistrationCommit);

                Storage.Verify(x => x.ReadPageAsync(It.IsAny<HiveType>(), It.IsAny<string>()), Times.Once);
                Storage.Verify(x => x.ReadPageAsync(Hive, pageUrl), Times.Once);
                Storage.Verify(
                    x => x.WriteLeafAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("6.0.0"), RegistrationLeaf),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<NuGetVersion>(), It.IsAny<RegistrationPage>()),
                    Times.Once);
                Storage.Verify(
                    x => x.WritePageAsync(Hive, ReplicaHives, Id, NuGetVersion.Parse("4.0.0"), NuGetVersion.Parse("7.0.0"), page),
                    Times.Once);
                Storage.Verify(
                    x => x.WriteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>(), It.IsAny<RegistrationIndex>()),
                    Times.Once);
                Storage.Verify(x => x.WriteIndexAsync(Hive, ReplicaHives, Id, RegistrationIndex), Times.Once);
                Storage.Verify(x => x.DeleteIndexAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Storage.Verify(x => x.DeleteUrlAsync(It.IsAny<HiveType>(), It.IsAny<IReadOnlyList<HiveType>>(), It.IsAny<string>()), Times.Never);
                Assert.Null(Assert.Single(RegistrationIndex.Items).Items);
                Assert.Equal(new[] { "4.0.0", "5.0.0", "6.0.0", "7.0.0" }, page.Items.Select(x => x.CatalogEntry.Version).ToArray());
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Storage = new Mock<IHiveStorage>();
                Merger = new Mock<IHiveMerger>();
                EntityBuilder = new Mock<IEntityBuilder>();
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Logger = output.GetLogger<HiveUpdater>();

                Config = new Catalog2RegistrationConfiguration();
                Hive = HiveType.SemVer2;
                ReplicaHives = new List<HiveType>();
                Id = "NuGet.Versioning";
                Entries = new List<CatalogCommitItem>();
                EntryToCatalogLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>(
                    ReferenceEqualityComparer<CatalogCommitItem>.Default);
                RegistrationIndex = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = "1.0.0",
                            Upper = "3.0.0",
                            Count = 2,
                            Items = new List<RegistrationLeafItem>
                            {
                                new RegistrationLeafItem
                                {
                                    Url = $"https://example/reg/{Id.ToLowerInvariant()}/1.0.0.json",
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Version = "1.0.0",
                                    }
                                },
                                new RegistrationLeafItem
                                {
                                    Url = $"https://example/reg/{Id.ToLowerInvariant()}/3.0.0.json",
                                    CatalogEntry = new RegistrationCatalogEntry
                                    {
                                        Version = "3.0.0",
                                    }
                                },
                            }
                        }
                    }
                };
                MergeResult = new HiveMergeResult(
                    new HashSet<PageInfo>(),
                    new HashSet<LeafInfo>(),
                    new HashSet<LeafInfo>());
                RegistrationLeaf = new RegistrationLeaf();
                RegistrationCommit = new CatalogCommit(
                    "b580f835-f041-4361-aa46-57e5dc338a63",
                    new DateTimeOffset(2019, 10, 25, 0, 0, 0, TimeSpan.Zero));

                Options.Setup(x => x.Value).Returns(() => Config);
                Storage
                    .Setup(x => x.ReadIndexOrNullAsync(It.IsAny<HiveType>(), It.IsAny<string>()))
                    .ReturnsAsync(() => RegistrationIndex);
                var concreteHiveMerger = new HiveMerger(Options.Object, output.GetLogger<HiveMerger>());
                Merger
                    .Setup(x => x.MergeAsync(It.IsAny<IndexInfo>(), It.IsAny<IReadOnlyList<CatalogCommitItem>>()))
                    .Returns<IndexInfo, IReadOnlyList<CatalogCommitItem>>((i, e) => concreteHiveMerger.MergeAsync(i, e));
                EntityBuilder
                    .Setup(x => x.NewLeaf(It.IsAny<RegistrationLeafItem>()))
                    .Returns(() => RegistrationLeaf);
                EntityBuilder
                    .Setup(x => x.UpdateNonInlinedPageItem(
                        It.IsAny<RegistrationPage>(),
                        It.IsAny<HiveType>(),
                        It.IsAny<string>(),
                        It.IsAny<int>(),
                        It.IsAny<NuGetVersion>(),
                        It.IsAny<NuGetVersion>()))
                    .Callback<RegistrationPage, HiveType, string, int, NuGetVersion, NuGetVersion>((p, h, id, c, l, u) =>
                    {
                        p.Url = $"https://example/reg/" +
                            $"{id.ToLowerInvariant()}/" +
                            $"{l.ToNormalizedString().ToLowerInvariant()}/" +
                            $"{u.ToNormalizedString().ToLowerInvariant()}.json";
                    });

                Target = new HiveUpdater(
                    Storage.Object,
                    Merger.Object,
                    EntityBuilder.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<IHiveStorage> Storage { get; }
            public Mock<IHiveMerger> Merger { get; }
            public Mock<IEntityBuilder> EntityBuilder { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public RecordingLogger<HiveUpdater> Logger { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public HiveType Hive { get; set; }
            public List<HiveType> ReplicaHives { get; }
            public string Id { get; }
            public List<CatalogCommitItem> Entries { get; }
            public Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> EntryToCatalogLeaf { get; }
            public RegistrationIndex RegistrationIndex { get; }
            public HiveMergeResult MergeResult { get; }
            public RegistrationLeaf RegistrationLeaf { get; }
            public CatalogCommit RegistrationCommit { get; }
            public HiveUpdater Target { get; }

            public void AddPackageDetails(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                var catalogCommitItem = GetCatalogCommitItem(parsedVersion, Schema.DataTypes.PackageDetails);
                Entries.Add(catalogCommitItem);
                EntryToCatalogLeaf[catalogCommitItem] = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = version,
                };
            }

            public void AddPackageDelete(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                var catalogCommitItem = GetCatalogCommitItem(parsedVersion, Schema.DataTypes.PackageDelete);
                Entries.Add(catalogCommitItem);

            }

            private CatalogCommitItem GetCatalogCommitItem(NuGetVersion parsedVersion, Uri typeUri)
            {
                return new CatalogCommitItem(
                    new Uri($"https://example/catalog/{Entries.Count}/{Id.ToLowerInvariant()}/{parsedVersion.ToNormalizedString().ToLowerInvariant()}.json"),
                    Entries.Count.ToString(),
                    new DateTime(2019, 10, 19, 0, 0, 0, DateTimeKind.Utc).AddHours(Entries.Count),
                    new List<string>(),
                    new List<Uri> { typeUri },
                    new PackageIdentity(Id, parsedVersion));
            }
        }
    }
}
