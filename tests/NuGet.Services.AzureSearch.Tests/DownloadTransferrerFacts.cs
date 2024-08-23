﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Auxiliary2AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class DownloadTransferrerFacts
    {
        public class InitializeDownloadTransfers : Facts
        {
            [Fact]
            public void ReturnsEmptyResult()
            {
                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Empty(result);
            }

            [Fact]
            public void DoesNothingIfNoTransfers()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Empty(result);
            }

            [Fact]
            public void TransfersPopularity()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(50, result["A"]);
                Assert.Equal(55, result["B"]);
            }

            [Fact]
            public void SplitsPopularity()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 1);

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(50, result["A"]);
                Assert.Equal(30, result["B"]);
                Assert.Equal(26, result["C"]);
            }

            [Fact]
            public void PopularityTransferRoundsDown()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 3);
                DownloadData.SetDownloadCount("B", "1.0.0", 0);

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(1, result["A"]);
                Assert.Equal(1, result["B"]);
            }

            [Fact]
            public void AcceptsPopularityFromMultipleSources()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 20);
                DownloadData.SetDownloadCount("C", "1.0.0", 1);

                PopularityTransfers.AddTransfer("A", "C");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
                Assert.Equal(121, result["C"]);
            }

            [Fact]
            public void SupportsZeroPopularityTransfer()
            {
                PopularityTransfer = 0;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(100, result["A"]);
                Assert.Equal(5, result["B"]);
            }

            [Fact]
            public void PackageWithOutgoingTransferRejectsIncomingTransfer()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 0);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                // B has incoming and outgoing popularity transfers. It should reject the incoming transfer.
                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
                Assert.Equal(50, result["C"]);
            }

            [Fact]
            public void PopularityTransfersAreNotTransitive()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 100);
                DownloadData.SetDownloadCount("C", "1.0.0", 100);

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                // A transfers downloads to B.
                // B transfers downloads to C.
                // B and C should reject downloads from A.
                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
                Assert.Equal(200, result["C"]);
            }

            [Fact]
            public void RejectsCyclicalPopularityTransfers()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 100);

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("B", "A");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
            }

            [Fact]
            public void UnknownPackagesTransferZeroDownloads()
            {
                PopularityTransfer = 1;

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
            }
        }

        public class GetUpdatedTransferChanges : Facts
        {
            [Fact]
            public void RequiresDownloadDataForDownloadChange()
            {
                DownloadChanges["A"] = 1;

                var ex = Assert.Throws<InvalidOperationException>(
                    () => Target.UpdateDownloadTransfers(
                        DownloadData,
                        DownloadChanges,
                        OldTransfers,
                        PopularityTransfers));

                Assert.Equal("The download changes should match the latest downloads", ex.Message);
            }

            [Fact]
            public void RequiresDownloadDataAndChangesMatch()
            {
                DownloadData.SetDownloadCount("A", "1.0.0", 1);
                DownloadChanges["A"] = 2;

                var ex = Assert.Throws<InvalidOperationException>(
                    () => Target.UpdateDownloadTransfers(
                        DownloadData,
                        DownloadChanges,
                        OldTransfers,
                        PopularityTransfers));

                Assert.Equal("The download changes should match the latest downloads", ex.Message);
            }

            [Fact]
            public void ReturnsEmptyResult()
            {
                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Empty(result);
            }

            [Fact]
            public void DoesNothingIfNoTransfers()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                DownloadChanges["A"] = 100;
                DownloadChanges["B"] = 5;

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Empty(result);
            }

            [Fact]
            public void DoesNothingIfNoChanges()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Empty(result);
            }

            [Fact]
            public void OutgoingTransfersNewDownloads()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 20);
                DownloadData.SetDownloadCount("C", "1.0.0", 1);

                DownloadChanges["A"] = 100;

                PopularityTransfers.AddTransfer("A", "C");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                // C receives downloads from A and B
                // A has download changes
                // B has no changes
                Assert.Equal(new[] { "A", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(121, result["C"]);
            }

            [Fact]
            public void OutgoingTransfersSplitsNewDownloads()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                DownloadChanges["A"] = 100;

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(55, result["B"]);
                Assert.Equal(50, result["C"]);
            }

            [Fact]
            public void PopularityTransferRoundsDown()
            {
                PopularityTransfer = 0.5;

                DownloadData.SetDownloadCount("A", "1.0.0", 3);
                DownloadData.SetDownloadCount("B", "1.0.0", 0);

                DownloadChanges["A"] = 3;

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(1, result["A"]);
                Assert.Equal(1, result["B"]);
            }

            [Fact]
            public void IncomingTransfersAddedToNewDownloads()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                DownloadChanges["B"] = 5;

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                // B has new downloads and receives downloads from A.
                Assert.Equal(new[] { "B" }, result.Keys);
                Assert.Equal(55, result["B"]);
            }

            [Fact]
            public void NewOrUpdatedPopularityTransfer()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                PopularityTransfers.AddTransfer("A", "B");

                TransferChanges["A"] = new[] { "B" };

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(105, result["B"]);
            }

            [Fact]
            public void NewOrUpdatedSplitsPopularityTransfer()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");

                TransferChanges["A"] = new[] { "B", "C" };

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(55, result["B"]);
                Assert.Equal(50, result["C"]);
            }

            [Fact]
            public void RemovesIncomingPopularityTransfer()
            {
                // A used to transfer to both B and C.
                // A now transfers to just B.
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                PopularityTransfers.AddTransfer("A", "B");

                TransferChanges["A"] = new[] { "B" };
                OldTransfers.AddTransfer("A", "B");
                OldTransfers.AddTransfer("A", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(105, result["B"]);
                Assert.Equal(0, result["C"]);
            }

            [Fact]
            public void RemovePopularityTransfer()
            {
                // A used to transfer to both B and C.
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                TransferChanges["A"] = new string[0];
                OldTransfers.AddTransfer("A", "B");
                OldTransfers.AddTransfer("A", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(100, result["A"]);
                Assert.Equal(5, result["B"]);
                Assert.Equal(0, result["C"]);
            }

            [Fact]
            public void SupportsZeroPopularityTransfer()
            {
                PopularityTransfer = 0;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 5);

                DownloadChanges["A"] = 100;

                PopularityTransfers.AddTransfer("A", "B");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(100, result["A"]);
                Assert.Equal(5, result["B"]);
            }

            [Fact]
            public void PackageWithOutgoingTransferRejectsIncomingTransfer()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 0);
                DownloadData.SetDownloadCount("C", "1.0.0", 0);

                DownloadChanges["A"] = 100;

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("A", "C");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                // B has incoming and outgoing popularity transfers. It should reject the incoming transfer.
                Assert.Equal(new[] { "A", "B", "C" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
                Assert.Equal(50, result["C"]);
            }

            [Fact]
            public void PopularityTransfersAreNotTransitive()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 100);
                DownloadData.SetDownloadCount("C", "1.0.0", 100);

                DownloadChanges["A"] = 100;

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("B", "C");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                // A transfers downloads to B.
                // B transfers downloads to C.
                // B and C should reject downloads from A.
                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
            }

            [Fact]
            public void RejectsCyclicalPopularityTransfers()
            {
                PopularityTransfer = 1;

                DownloadData.SetDownloadCount("A", "1.0.0", 100);
                DownloadData.SetDownloadCount("B", "1.0.0", 100);

                DownloadChanges["A"] = 100;
                DownloadChanges["B"] = 100;

                PopularityTransfers.AddTransfer("A", "B");
                PopularityTransfers.AddTransfer("B", "A");

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers);

                Assert.Equal(new[] { "A", "B" }, result.Keys);
                Assert.Equal(0, result["A"]);
                Assert.Equal(0, result["B"]);
            }

            public GetUpdatedTransferChanges()
            {
                DownloadChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                OldTransfers = new PopularityTransferData();
            }

            public SortedDictionary<string, long> DownloadChanges { get; }
            public PopularityTransferData OldTransfers { get; }
        }

        public class Facts
        {
            public Facts()
            {
                TransferChanges = new SortedDictionary<string, string[]>();
                DataComparer = new Mock<IDataSetComparer>();
                DataComparer
                    .Setup(x => x.ComparePopularityTransfers(
                        It.IsAny<PopularityTransferData>(),
                        It.IsAny<PopularityTransferData>()))
                    .Returns(TransferChanges);

                PopularityTransfers = new PopularityTransferData();

                var options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                options
                    .Setup(x => x.Value)
                    .Returns(() => new AzureSearchJobConfiguration
                    {
                        Scoring = new AzureSearchScoringConfiguration
                        {
                            PopularityTransfer = PopularityTransfer
                        }
                    });

                DownloadData = new DownloadData();

                Target = new DownloadTransferrer(
                    DataComparer.Object,
                    options.Object,
                    Mock.Of<ILogger<DownloadTransferrer>>());
            }

            public Mock<IDataSetComparer> DataComparer { get; }
            public IDownloadTransferrer Target { get; }

            public DownloadData DownloadData { get; }
            public PopularityTransferData PopularityTransfers { get; }
            public SortedDictionary<string, string[]> TransferChanges { get; }
            public double PopularityTransfer = 0;
        }
    }
}
