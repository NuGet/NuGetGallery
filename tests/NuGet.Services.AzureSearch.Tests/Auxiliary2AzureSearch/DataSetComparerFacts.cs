// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class DataSetComparerFacts
    {
        public class CompareOwners : Facts
        {
            public CompareOwners(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void FindsAddedPackageIds()
            {
                var oldData = OwnersData("NuGet.Core: NuGet, Microsoft");
                var newData = OwnersData(
                    "NuGet.Core: NuGet, Microsoft",
                    "NuGet.Versioning: NuGet, Microsoft");

                var changes = Target.CompareOwners(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Versioning", pair.Key);
                Assert.Equal(new[] { "Microsoft", "NuGet" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 2,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsRemovedPackageIds()
            {
                var oldData = OwnersData(
                    "NuGet.Core: NuGet, Microsoft",
                    "NuGet.Versioning: NuGet, Microsoft");
                var newData = OwnersData("NuGet.Core: NuGet, Microsoft");

                var changes = Target.CompareOwners(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Versioning", pair.Key);
                Assert.Empty(pair.Value);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 2,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsAddedOwner()
            {
                var oldData = OwnersData("NuGet.Core: NuGet");
                var newData = OwnersData("NuGet.Core: NuGet, Microsoft");

                var changes = Target.CompareOwners(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "Microsoft", "NuGet" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsRemovedOwner()
            {
                var oldData = OwnersData("NuGet.Core: NuGet, Microsoft");
                var newData = OwnersData("NuGet.Core: NuGet");

                var changes = Target.CompareOwners(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "NuGet" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsOwnerWithChangedCase()
            {
                var oldData = OwnersData("NuGet.Core: NuGet, Microsoft");
                var newData = OwnersData("NuGet.Core: NuGet, microsoft");

                var changes = Target.CompareOwners(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "microsoft", "NuGet" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsManyChangesAtOnce()
            {
                var oldData = OwnersData(
                    "NuGet.Core: NuGet, Microsoft",
                    "NuGet.Frameworks: NuGet",
                    "NuGet.Protocol: NuGet");
                var newData = OwnersData(
                    "NuGet.Core: NuGet, microsoft",
                    "NuGet.Versioning: NuGet",
                    "NuGet.Protocol: NuGet");

                var changes = Target.CompareOwners(oldData, newData);

                Assert.Equal(3, changes.Count);
                Assert.Equal(new[] { "NuGet.Core", "NuGet.Frameworks", "NuGet.Versioning" }, changes.Keys.ToArray());
                Assert.Equal(new[] { "microsoft", "NuGet" }, changes["NuGet.Core"]);
                Assert.Empty(changes["NuGet.Frameworks"]);
                Assert.Equal(new[] { "NuGet" }, changes["NuGet.Versioning"]);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 3,
                        /*newCount: */ 3,
                        /*changeCount: */ 3,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsNoChanges()
            {
                var oldData = OwnersData(
                    "NuGet.Core: NuGet, Microsoft",
                    "NuGet.Versioning: NuGet, Microsoft");
                var newData = OwnersData(
                    "NuGet.Core: NuGet, Microsoft",
                    "NuGet.Versioning: NuGet, Microsoft");

                var changes = Target.CompareOwners(oldData, newData);

                Assert.Empty(changes);

                TelemetryService.Verify(
                    x => x.TrackOwnerSetComparison(
                        /*oldCount: */ 2,
                        /*newCount: */ 2,
                        /*changeCount: */ 0,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }
        }

        public class ComparePopularityTransfers : Facts
        {
            public ComparePopularityTransfers(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void FindsNoChanges()
            {
                var oldData = TransfersData(
                    "PackageA: PackageB, PackageC",
                    "Package1: Package3, Package2");
                var newData = TransfersData(
                    "PackageA: PackageC, PackageB",
                    "Package1: Package2, Package3");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                Assert.Empty(changes);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 2,
                        /*newCount: */ 2,
                        /*changeCount: */ 0,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsAddedTransfers()
            {
                var oldData = TransfersData("PackageA: PackageB, PackageC");
                var newData = TransfersData(
                    "PackageA: PackageB, PackageC",
                    "Package1: Package2, Package3");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("Package1", pair.Key);
                Assert.Equal(new[] { "Package2", "Package3" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 2,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsRemovedTransfers()
            {
                var oldData = TransfersData(
                    "PackageA: PackageB, PackageC",
                    "Package1: Package2, Package3");
                var newData = TransfersData("PackageA: PackageB, PackageC");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("Package1", pair.Key);
                Assert.Empty(pair.Value);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 2,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsAddedToPackage()
            {
                var oldData = TransfersData("PackageA: PackageB");
                var newData = TransfersData("PackageA: PackageB, PackageC");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("PackageA", pair.Key);
                Assert.Equal(new[] { "PackageB", "PackageC" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsRemovedToPackage()
            {
                var oldData = TransfersData("PackageA: PackageB, PackageC");
                var newData = TransfersData("PackageA: PackageB");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("PackageA", pair.Key);
                Assert.Equal(new[] { "PackageB" }, pair.Value);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 1,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void IgnoresCaseChanges()
            {
                var oldData = TransfersData("PackageA: packageb, PackageC");
                var newData = TransfersData("packagea: PACKAGEB, packageC");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                Assert.Empty(changes);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 1,
                        /*newCount: */ 1,
                        /*changeCount: */ 0,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }

            [Fact]
            public void FindsManyChangesAtOnce()
            {
                var oldData = TransfersData(
                    "Package1: PackageA, PackageB",
                    "Package2: PackageC",
                    "Package3: PackageD");
                var newData = TransfersData(
                    "Package1: PackageA, PackageE",
                    "Package4: PackageC",
                    "Package3: Packaged");

                var changes = Target.ComparePopularityTransfers(oldData, newData);

                Assert.Equal(3, changes.Count);
                Assert.Equal(new[] { "Package1", "Package2", "Package4" }, changes.Keys.ToArray());
                Assert.Equal(new[] { "PackageA", "PackageE" }, changes["Package1"]);
                Assert.Empty(changes["Package2"]);
                Assert.Equal(new[] { "PackageC" }, changes["Package4"]);

                TelemetryService.Verify(
                    x => x.TrackPopularityTransfersSetComparison(
                        /*oldCount: */ 3,
                        /*newCount: */ 3,
                        /*changeCount: */ 3,
                        It.IsAny<TimeSpan>()),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<DataSetComparer>();

                Target = new DataSetComparer(
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<DataSetComparer> Logger { get; }
            public DataSetComparer Target { get; }

            /// <summary>
            /// A helper to turn lines formatted like this "PackageId: OwnerA, OwnerB" into package ID to owners
            /// dictionary.
            /// </summary>
            public SortedDictionary<string, SortedSet<string>> OwnersData(params string[] lines)
            {
                var builder = new PackageIdToOwnersBuilder(Logger);
                ParseData(lines, builder.Add);
                return builder.GetResult();
            }

            /// <summary>
            /// A helper to turn lines formatted like this "FromPackage1: ToPackage1, ToPackage2" into package ID to popularity
            /// transfers dictionary.
            /// </summary>
            public SortedDictionary<string, SortedSet<string>> TransfersData(params string[] lines)
            {
                var builder = new PackageIdToPopularityTransfersBuilder(Logger);
                ParseData(lines, builder.Add);
                return builder.GetResult();
            }

            private void ParseData(string[] lines, Action<string, List<string>> add)
            {
                foreach (var line in lines)
                {
                    var pieces = line.Split(new[] { ':' }, 2);
                    var key = pieces[0].Trim();
                    var values = pieces[1]
                        .Split(',')
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .ToList();

                    add(key, values);
                }
            }
        }
    }
}
