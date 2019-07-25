// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class PackageEntityIndexActionBuilderFacts
    {
        public class AddNewPackageRegistration : BaseFacts
        {
            public AddNewPackageRegistration(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData("1.0.0.0-ALPHA", "1.0.0-ALPHA")]
            [InlineData("01.0.0-ALPHA", "1.0.0-ALPHA")]
            [InlineData("1.0.0-ALPHA+githash", "1.0.0-ALPHA+githash")]
            [InlineData("1.0.0-ALPHA.1+githash", "1.0.0-ALPHA.1+githash")]
            public void UsesProperVersionForBuilder(string version, string fullVersion)
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage(version) { SemVerLevelKey = SemVerLevelKey.SemVer2 } },
                    false);

                var actions = _target.AddNewPackageRegistration(input);

                _search.Verify(
                    x => x.Keyed(input.PackageId, SearchFilters.Default),
                    Times.Once);
                _search.Verify(
                    x => x.Keyed(input.PackageId, SearchFilters.IncludePrerelease),
                    Times.Once);
                _search.Verify(
                    x => x.Keyed(input.PackageId, SearchFilters.IncludeSemVer2),
                    Times.Once);
                _search.Verify(
                    x => x.FullFromDb(
                        input.PackageId,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        fullVersion,
                        input.Packages[0],
                        input.Owners,
                        input.TotalDownloadCount,
                        It.IsAny<bool>()),
                    Times.Once);
            }

            [Fact]
            public void UsesLatestVersionMetadataForSearchIndex()
            {
                _search
                    .Setup(x => x.FullFromDb(
                        It.IsAny<string>(),
                        It.IsAny<SearchFilters>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<Package>(),
                        It.IsAny<string[]>(),
                        It.IsAny<long>(),
                        It.IsAny<bool>()))
                    .Returns<string, SearchFilters, string[], bool, bool, string, Package, string[], long, bool>(
                        (i, sf, v, ls, l, fv, p, o, d, ie) => new SearchDocument.Full { OriginalVersion = p.Version });
                var package1 = new TestPackage("1.0.0") { Description = "This is version 1.0.0." };
                var package2 = new TestPackage("2.0.0-alpha") { Description = "This is version 2.0.0." };
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { package1, package2 },
                    false);

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Upload, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Upload, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Upload, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.Equal(IndexActionType.Upload, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
                var doc0 = Assert.IsType<SearchDocument.Full>(actions.Search[0].Document);
                Assert.Equal(package1.Version, doc0.OriginalVersion);
                var doc1 = Assert.IsType<SearchDocument.Full>(actions.Search[1].Document);
                Assert.Equal(package2.Version, doc1.OriginalVersion);
                var doc2 = Assert.IsType<SearchDocument.Full>(actions.Search[2].Document);
                Assert.Equal(package1.Version, doc2.OriginalVersion);
                var doc3 = Assert.IsType<SearchDocument.Full>(actions.Search[3].Document);
                Assert.Equal(package2.Version, doc3.OriginalVersion);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void PassesIsExcludedByDefaultValueCorrectly(bool shouldBeExcluded)
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") { SemVerLevelKey = SemVerLevelKey.SemVer2 } },
                    isExcludedByDefault: shouldBeExcluded);

                var actions = _target.AddNewPackageRegistration(input);

                _search.Verify(
                    x => x.FullFromDb(
                        input.PackageId,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        input.Packages[0],
                        input.Owners,
                        input.TotalDownloadCount,
                        shouldBeExcluded),
                    Times.Once);
            }

            [Fact]
            public void ReturnsDeleteSearchActionsForAllUnlisted()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") { Listed = false } },
                    false);

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Delete, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Delete, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Delete, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.Equal(IndexActionType.Delete, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
            }

            [Fact]
            public void UsesSemVerLevelToIndicateSemVer2()
            {
                _search
                    .Setup(x => x.FullFromDb(
                        It.IsAny<string>(),
                        It.IsAny<SearchFilters>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<Package>(),
                        It.IsAny<string[]>(),
                        It.IsAny<long>(),
                        It.IsAny<bool>()))
                    .Returns(() => new SearchDocument.Full());
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") { SemVerLevelKey = SemVerLevelKey.SemVer2 } },
                    false);

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Delete, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Delete, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Upload, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.IsType<SearchDocument.Full>(actions.Search[2].Document);
                Assert.Equal(IndexActionType.Upload, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
                Assert.IsType<SearchDocument.Full>(actions.Search[3].Document);
            }
        }

        /// <summary>
        /// Provides a convenience constructor that initialized version-related properties in a consistent manner.
        /// </summary>
        public class TestPackage : Package
        {
            public TestPackage(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                Version = version;
                NormalizedVersion = parsedVersion.ToNormalizedString();
                IsPrerelease = parsedVersion.IsPrerelease;
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<ISearchDocumentBuilder> _search;
            protected readonly Mock<IHijackDocumentBuilder> _hijack;
            protected readonly RecordingLogger<PackageEntityIndexActionBuilder> _logger;
            protected readonly PackageEntityIndexActionBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _search = new Mock<ISearchDocumentBuilder>();
                _hijack = new Mock<IHijackDocumentBuilder>();
                _logger = output.GetLogger<PackageEntityIndexActionBuilder>();

                _search
                    .Setup(x => x.LatestFlagsOrNull(It.IsAny<VersionLists>(), It.IsAny<SearchFilters>()))
                    .Returns<VersionLists, SearchFilters>((vl, sf) => new SearchDocument.LatestFlags(
                        vl.GetLatestVersionInfoOrNull(sf),
                        isLatestStable: true,
                        isLatest: true));

                _target = new PackageEntityIndexActionBuilder(
                    _search.Object,
                    _hijack.Object,
                    _logger);
            }
        }
    }
}
