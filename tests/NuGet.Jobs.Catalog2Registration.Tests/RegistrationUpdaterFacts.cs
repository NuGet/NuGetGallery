// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationUpdaterFacts
    {
        public class UpdateAsync : Facts
        {
            public UpdateAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task UsesDifferentCommitIdButSameCommitTimestamp()
            {
                var commits = new ConcurrentBag<CatalogCommit>();
                HiveUpdater
                    .Setup(x => x.UpdateAsync(
                        It.IsAny<HiveType>(),
                        It.IsAny<IReadOnlyList<HiveType>>(),
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(),
                        It.IsAny<CatalogCommit>()))
                    .Returns(Task.CompletedTask)
                    .Callback<HiveType, IReadOnlyList<HiveType>, string, IReadOnlyList<CatalogCommitItem>, IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>, CatalogCommit>(
                        (h, r, i, e, l, c) => commits.Add(c));

                await Target.UpdateAsync(Id, Entries, EntryToLeaf);

                Assert.Equal(2, commits.Count);
                Assert.Single(commits.Select(x => x.Timestamp).Distinct());
                Assert.Equal(2, commits.Select(x => x.Id).Distinct().Count());
            }

            [Fact]
            public async Task UsesProperReplicaHives()
            {
                await Target.UpdateAsync(Id, Entries, EntryToLeaf);

                HiveUpdater.Verify(
                    x => x.UpdateAsync(
                        It.IsAny<HiveType>(),
                        It.IsAny<IReadOnlyList<HiveType>>(),
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyList<CatalogCommitItem>>(),
                        It.IsAny<IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>>(),
                        It.IsAny<CatalogCommit>()),
                    Times.Exactly(2));
                HiveUpdater.Verify(
                    x => x.UpdateAsync(
                        HiveType.Legacy,
                        It.Is<IReadOnlyList<HiveType>>(r => r.Count == 1 && r[0] == HiveType.Gzipped),
                        Id,
                        Entries,
                        EntryToLeaf,
                        It.IsAny<CatalogCommit>()),
                    Times.Once);
                HiveUpdater.Verify(
                    x => x.UpdateAsync(
                        HiveType.SemVer2,
                        It.Is<IReadOnlyList<HiveType>>(r => r.Count == 0),
                        Id,
                        Entries,
                        EntryToLeaf,
                        It.IsAny<CatalogCommit>()),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                HiveUpdater = new Mock<IHiveUpdater>();
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Logger = output.GetLogger<RegistrationUpdater>();

                Config = new Catalog2RegistrationConfiguration();
                Config.MaxConcurrentHivesPerId = 1;
                Id = "NuGet.Versioning";
                Entries = new List<CatalogCommitItem>();
                EntryToLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>();

                Options.Setup(x => x.Value).Returns(() => Config);

                Target = new RegistrationUpdater(
                    HiveUpdater.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<IHiveUpdater> HiveUpdater { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public RecordingLogger<RegistrationUpdater> Logger { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public string Id { get; }
            public List<CatalogCommitItem> Entries { get; }
            public Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> EntryToLeaf { get; }
            public RegistrationUpdater Target { get; }
        }
    }
}
