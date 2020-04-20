// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Empty(result);
            }

            [Fact]
            public void AppliesDownloadOverrides()
            {
                DownloadData.SetDownloadCount("A", "1.0.0", 1);
                DownloadData.SetDownloadCount("B", "1.0.0", 2);

                DownloadOverrides["A"] = 1000;

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Equal(new[] { "A" }, result.Keys);
                Assert.Equal(1000, result["A"]);
            }

            [Fact]
            public void DoesNotOverrideGreaterOrEqualDownloads()
            {
                DownloadData.SetDownloadCount("A", "1.0.0", 1000);
                DownloadData.SetDownloadCount("B", "1.0.0", 1000);

                DownloadOverrides["A"] = 1;
                DownloadOverrides["B"] = 1000;

                var result = Target.InitializeDownloadTransfers(
                    DownloadData,
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Empty(result);
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
                        PopularityTransfers,
                        DownloadOverrides));

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
                        PopularityTransfers,
                        DownloadOverrides));

                Assert.Equal("The download changes should match the latest downloads", ex.Message);
            }

            [Fact]
            public void ReturnsEmptyResult()
            {
                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Empty(result);
            }

            [Fact]
            public void AppliesDownloadOverrides()
            {
                DownloadData.SetDownloadCount("A", "1.0.0", 1);
                DownloadData.SetDownloadCount("B", "1.0.0", 2);
                DownloadData.SetDownloadCount("C", "1.0.0", 3);
                DownloadData.SetDownloadCount("D", "1.0.0", 4);

                DownloadChanges["C"] = 3;
                DownloadChanges["D"] = 4;

                DownloadOverrides["A"] = 1000;
                DownloadOverrides["C"] = 3000;

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Equal(2, result.Count);
                Assert.Equal(new[] { "A", "C" }, result.Keys);
                Assert.Equal(1000, result["A"]);
                Assert.Equal(3000, result["C"]);
            }

            [Fact]
            public void DoesNotOverrideGreaterOrEqualDownloads()
            {
                DownloadData.SetDownloadCount("A", "1.0.0", 1000);
                DownloadData.SetDownloadCount("B", "1.0.0", 1000);
                DownloadData.SetDownloadCount("C", "1.0.0", 1000);

                DownloadChanges["C"] = 1000;

                DownloadOverrides["A"] = 1;
                DownloadOverrides["B"] = 1000;
                DownloadOverrides["B"] = 1;

                var result = Target.UpdateDownloadTransfers(
                    DownloadData,
                    DownloadChanges,
                    OldTransfers,
                    PopularityTransfers,
                    DownloadOverrides);

                Assert.Empty(result);
            }

            public GetUpdatedTransferChanges()
            {
                DownloadChanges = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                OldTransfers = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            }

            public SortedDictionary<string, long> DownloadChanges { get; }
            public SortedDictionary<string, SortedSet<string>> OldTransfers { get; }
        }

        public class Facts
        {
            public Facts()
            {
                TransferChanges = new SortedDictionary<string, string[]>();

                DownloadOverrides = new Dictionary<string, long>();
                PopularityTransfers = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

                DownloadData = new DownloadData();

                Target = new DownloadTransferrer(
                    Mock.Of<ILogger<DownloadTransferrer>>());
            }

            public Mock<IDataSetComparer> DataComparer { get; }
            public IDownloadTransferrer Target { get; }

            public DownloadData DownloadData { get; }
            public Dictionary<string, long> DownloadOverrides { get; }
            public SortedDictionary<string, SortedSet<string>> PopularityTransfers { get; }
            public SortedDictionary<string, string[]> TransferChanges { get; }
            public double PopularityTransfer = 0;

            public void AddPopularityTransfer(string fromPackageId, string toPackageId)
            {
                if (!PopularityTransfers.TryGetValue(fromPackageId, out var toPackageIds))
                {
                    toPackageIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    PopularityTransfers[fromPackageId] = toPackageIds;
                }

                toPackageIds.Add(toPackageId);
            }
        }
    }
}