// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
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
                OldData = new PopularityTransferData();
                NewData = new PopularityTransferData();
            }

            public PopularityTransferData OldData { get; }
            public PopularityTransferData NewData { get; }

            [Fact]
            public void FindsNoChanges()
            {
                OldData.AddTransfer("PackageA", "PackageB");
                OldData.AddTransfer("PackageA", "PackageC");
                OldData.AddTransfer("Package1", "Package3");
                OldData.AddTransfer("Package1", "Package2");

                NewData.AddTransfer("PackageA", "PackageC");
                NewData.AddTransfer("PackageA", "PackageB");
                NewData.AddTransfer("Package1", "Package2");
                NewData.AddTransfer("Package1", "Package3");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("PackageA", "PackageB");
                OldData.AddTransfer("PackageA", "PackageC");

                NewData.AddTransfer("PackageA", "PackageB");
                NewData.AddTransfer("PackageA", "PackageC");
                NewData.AddTransfer("Package1", "Package2");
                NewData.AddTransfer("Package1", "Package3");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("PackageA", "PackageB");
                OldData.AddTransfer("PackageA", "PackageC");
                OldData.AddTransfer("Package1", "Package2");
                OldData.AddTransfer("Package1", "Package3");

                NewData.AddTransfer("PackageA", "PackageB");
                NewData.AddTransfer("PackageA", "PackageC");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("PackageA", "PackageB");

                NewData.AddTransfer("PackageA", "PackageB");
                NewData.AddTransfer("PackageA", "PackageC");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("PackageA", "PackageB");
                OldData.AddTransfer("PackageA", "PackageC");

                NewData.AddTransfer("PackageA", "PackageB");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("PackageA", "packageb");
                OldData.AddTransfer("PackageA", "PackageC");

                NewData.AddTransfer("packagea", "PACKAGEB");
                NewData.AddTransfer("PackageA", "packageC");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
                OldData.AddTransfer("Package1", "PackageA");
                OldData.AddTransfer("Package1", "PackageB");
                OldData.AddTransfer("Package2", "PackageC");
                OldData.AddTransfer("Package3", "PackageD");

                NewData.AddTransfer("Package1", "PackageA");
                NewData.AddTransfer("Package1", "PackageE");
                NewData.AddTransfer("Package4", "PackageC");
                NewData.AddTransfer("Package3", "Packaged");

                var changes = Target.ComparePopularityTransfers(OldData, NewData);

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
