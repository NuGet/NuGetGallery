// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public partial class HiveMergerFacts
    {
        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Logger = output.GetLogger<HiveMerger>();

                Config = new Catalog2RegistrationConfiguration
                {
                    MaxLeavesPerPage = 3,
                };
                Options.Setup(x => x.Value).Returns(() => Config);

                Target = new HiveMerger(
                    Options.Object,
                    Logger);
            }

            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public RecordingLogger<HiveMerger> Logger { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public HiveMerger Target { get; }
        }

        private class VersionAction
        {
            public VersionAction(NuGetVersion version, bool isDelete)
            {
                Version = version;
                IsDelete = isDelete;
            }

            public NuGetVersion Version { get; }
            public bool IsDelete { get; }
        }

        private class TestCase
        {
            public TestCase(List<NuGetVersion> existing, List<VersionAction> updated)
            {
                Existing = existing;
                Updated = updated;
            }

            public List<NuGetVersion> Existing { get; }
            public List<VersionAction> Updated { get; }
        }

        private static VersionAction Details(string version)
        {
            return new VersionAction(NuGetVersion.Parse(version), isDelete: false);
        }

        private static VersionAction Delete(string version)
        {
            return new VersionAction(NuGetVersion.Parse(version), isDelete: true);
        }

        private static string[] GetVersionArray(HashSet<LeafInfo> leafInfos)
        {
            return leafInfos
                .OrderBy(x => x.Version)
                .Select(x => x.LeafItem.CatalogEntry.Version)
                .ToArray();
        }

        private static async Task<string[]> GetVersionArrayAsync(IndexInfo indexInfo)
        {
            var versions = await GetVersionsAsync(indexInfo);
            return versions.Select(x => x.ToNormalizedString()).ToArray();
        }

        private static async Task<List<NuGetVersion>> GetVersionsAsync(IndexInfo indexInfo)
        {
            var versions = new List<NuGetVersion>();
            foreach (var pageInfo in indexInfo.Items)
            {
                var leafInfos = await pageInfo.GetLeafInfosAsync();
                foreach (var leafInfo in leafInfos)
                {
                    versions.Add(leafInfo.Version);
                }
            }

            return versions;
        }

        private static List<CatalogCommitItem> MakeSortedCatalog(params VersionAction[] versions)
        {
            return MakeSortedCatalog((ICollection<VersionAction>)versions);
        }

        private static List<CatalogCommitItem> MakeSortedCatalog(ICollection<VersionAction> sortedVersionActions)
        {
            var output = new List<CatalogCommitItem>();
            foreach (var versionAction in sortedVersionActions)
            {
                var item = new CatalogCommitItem(
                    uri: null,
                    commitId: null,
                    commitTimeStamp: DateTime.MinValue,
                    types: null,
                    typeUris: new[] { versionAction.IsDelete ? Schema.DataTypes.PackageDelete : Schema.DataTypes.PackageDetails },
                    packageIdentity: new PackageIdentity("NuGet.Versioning", versionAction.Version));

                output.Add(item);
            }

            return output;
        }

        private static IndexInfo MakeIndexInfo(params string[] versions)
        {
            var sortedVersions = versions.Select(x => NuGetVersion.Parse(x)).ToList();
            var versionToNormalized = sortedVersions.ToDictionary(x => x, x => x.ToNormalizedString());

            return MakeIndexInfo(sortedVersions, maxLeavesPerPage: 3, versionToNormalized: versionToNormalized);
        }

        private static IndexInfo MakeIndexInfo(
            List<NuGetVersion> sortedVersions,
            int maxLeavesPerPage,
            Dictionary<NuGetVersion, string> versionToNormalized)
        {
            var index = new RegistrationIndex
            {
                Items = new List<RegistrationPage>(),
            };

            // Populate the pages.
            RegistrationPage currentPage = null;
            for (var i = 0; i < sortedVersions.Count; i++)
            {
                if (i % maxLeavesPerPage == 0)
                {
                    currentPage = new RegistrationPage
                    {
                        Items = new List<RegistrationLeafItem>(),
                    };
                    index.Items.Add(currentPage);
                }

                currentPage.Items.Add(new RegistrationLeafItem
                {
                    CatalogEntry = new RegistrationCatalogEntry
                    {
                        Version = versionToNormalized[sortedVersions[i]],
                    },
                });
            }

            // Update the bounds.
            foreach (var page in index.Items)
            {
                page.Count = page.Items.Count;
                page.Lower = page.Items.First().CatalogEntry.Version;
                page.Upper = page.Items.Last().CatalogEntry.Version;
            }

            return IndexInfo.Existing(storage: null, hive: HiveType.SemVer2, index: index);
        }
    }
}
