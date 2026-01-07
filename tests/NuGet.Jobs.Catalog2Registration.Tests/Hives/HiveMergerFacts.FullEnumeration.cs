// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.V3.Support;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public partial class HiveMergerFacts
    {
        public class FullEnumeration
        {
            private readonly ITestOutputHelper _output;

            public FullEnumeration(ITestOutputHelper output)
            {
                _output = output;
            }

            [Theory]
            [InlineData(1, 2, 4, 1, 7)] // Use page size 1 to try some extreme prepending cases.
            [InlineData(2, 2, 6, 1, 7)] // Use page size 2 so we can easily have up to 3 pages.
            [InlineData(3, 2, 5, 1, 6)] // Use page size 3 to allow insertions without bounds changes.
            public async Task Execute(int maxLeavesPerPage, int minExisting, int maxExisting, int minUpdated, int maxUpdated)
            {
                var config = new Catalog2RegistrationConfiguration();
                var options = new SimpleOptions<Catalog2RegistrationConfiguration>(config);
                var logger = new NullLogger<HiveMerger>();
                var target = new HiveMerger(options, logger);

                config.MaxLeavesPerPage = maxLeavesPerPage;

                var allExisting = Enumerable.Range(minExisting, (maxExisting - minExisting) + 1).Select(m => new NuGetVersion(m, 0, 0)).ToList();
                var allUpdated = Enumerable.Range(minUpdated, (maxUpdated - minUpdated) + 1).Select(m => new NuGetVersion(m, 0, 0)).ToList();
                var versionToNormalized = allExisting.Concat(allUpdated).Distinct().ToDictionary(v => v, v => v.ToNormalizedString());

                // Enumerate all of the updated version sets, which is up to 9 versions and for each set every
                // combination of either PackageDelete or PackageDetails for each version.
                var deleteOrNotDelete = new[] { true, false };
                var updatedCases = IterTools
                    .SubsetsOf(allUpdated)
                    .SelectMany(vs => IterTools.CombinationsOfTwo(vs.ToList(), deleteOrNotDelete))
                    .Select(ts => ts.Select(t => new VersionAction(t.Item1, t.Item2)).OrderBy(v => v.Version).ToList())
                    .ToList();

                // Enumerate all of the updated version sets, which is up to 7 versions.
                var existingCases = IterTools
                    .SubsetsOf(allExisting)
                    .Select(vs => vs.OrderBy(v => v).ToList())
                    .ToList();

                // Build all of the test cases which is every pairing of updated and existing cases.
                var testCases = new ConcurrentBag<TestCase>();
                foreach (var existing in existingCases)
                {
                    foreach (var updated in updatedCases)
                    {
                        testCases.Add(new TestCase(existing, updated));
                    }
                }

                var completed = 0;
                var total = testCases.Count;
                var outputLock = new object();
                await ParallelAsync
                    .Repeat(async () =>
                    {
                        await Task.Yield();
                        while (testCases.TryTake(out var testCase))
                        {
                            try
                            {
                                await ExecuteTestCaseAsync(config, target, versionToNormalized, testCase);

                                var thisCompleted = Interlocked.Increment(ref completed);
                                if (thisCompleted % 20000 == 0)
                                {
                                    lock (outputLock)
                                    {
                                        _output.WriteLine($"Progress: {1.0 * thisCompleted / total:P}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                lock (outputLock)
                                {
                                    _output.WriteLine(string.Empty);
                                    _output.WriteLine("Test Case Failure");
                                    _output.WriteLine(new string('=', 50));
                                    _output.WriteLine(ex.Message);
                                    _output.WriteLine(string.Empty);
                                    _output.WriteLine("Existing: " + string.Join(", ", testCase.Existing));
                                    _output.WriteLine("Changes:  " + string.Join(", ", testCase.Updated.Select(t => $"{(t.IsDelete ? '-' : '+')}{t.Version}")));
                                    _output.WriteLine(new string('=', 50));
                                    _output.WriteLine(string.Empty);
                                    return;
                                }
                            }
                        }
                    },
                    degreeOfParallelism: 8);

                _output.WriteLine($"Progress: {1.0 * completed / total:P}");
                _output.WriteLine($"Total test cases: {total}");
                _output.WriteLine($"Completed test cases: {completed}");
                Assert.Equal(total, completed);
            }

            private async Task ExecuteTestCaseAsync(
                Catalog2RegistrationConfiguration config,
                HiveMerger target,
                Dictionary<NuGetVersion, string> versionToNormalized,
                TestCase testCase)
            {
                // ARRANGE
                var existing = testCase.Existing;
                var allUpdated = testCase.Updated;

                // Determine the sets of expected modified versions, deleted versions, and resulting versions.
                var updatedGrouped = allUpdated.ToLookup(x => x.IsDelete);
                var possibleDeletes = updatedGrouped[true].Select(x => x.Version);
                var modified = updatedGrouped[false].Select(x => x.Version).OrderBy(v => v).ToList();
                var deleted = existing.Intersect(possibleDeletes).OrderBy(v => v).ToList();
                var unchanged = existing.Except(allUpdated.Select(x => x.Version)).OrderBy(v => v).ToList();
                var expectedRemaining = modified.Concat(unchanged).OrderBy(v => v).ToList();

                // Build the input data structures.
                var sortedCatalog = MakeSortedCatalog(allUpdated);
                var indexInfo = MakeIndexInfo(existing, config.MaxLeavesPerPage, versionToNormalized);

                // Determine which pages will definitely not be affected.
                var unaffectedPages = new List<PageInfo>();
                if (existing.Any())
                {
                    var minVersion = allUpdated.Min(x => x.Version);
                    unaffectedPages = indexInfo
                        .Items
                        .Take(indexInfo.Items.Count - 1)
                        .Where(x => x.Upper < minVersion)
                        .ToList();
                }

                // ACT
                var result = await target.MergeAsync(indexInfo, sortedCatalog);

                // ASSERT

                // Verify the definitely unaffected pages are in still in the index and not loaded.
                foreach (var page in unaffectedPages)
                {
                    Assert.Contains(page, indexInfo.Items);
                    Assert.False(page.IsPageFetched, "A page before the lowest version input version must not be fetched.");
                }

                // Verify the modified version set.
                Assert.Equal(modified, result.ModifiedLeaves.Select(x => x.Version).OrderBy(v => v).ToList());

                // Verify the deleted version set.
                Assert.Equal(deleted, result.DeletedLeaves.Select(x => x.Version).OrderBy(v => v).ToList());

                // Verify the resulting set of versions.
                var actualRemaining = await GetVersionsAsync(indexInfo);
                Assert.Equal(expectedRemaining, actualRemaining);

                // Verify all but the last page are full.
                foreach (var pageInfo in indexInfo.Items.AsEnumerable().Reverse().Skip(1))
                {
                    Assert.True(
                        pageInfo.Count == config.MaxLeavesPerPage,
                        "All but the last page must have the maximum number of leaf items per page.");
                }

                // Verify the last page is not too full.
                if (indexInfo.Items.Count > 0)
                {
                    Assert.True(
                        indexInfo.Items.Last().Count <= config.MaxLeavesPerPage,
                        "The last page must have less than or equal to the maximum number of leaf items per page.");
                }

                // Verify the page bounds are in ascending order.
                var bounds = indexInfo.Items.SelectMany(GetUniqueBounds).ToList();
                for (var i = 1; i < bounds.Count; i++)
                {
                    Assert.True(bounds[i - 1] < bounds[i], "Each page bound must be less than the next.");
                }

                // Verify the modified pages are in the index.
                foreach (var pageInfo in result.ModifiedPages)
                {
                    Assert.True(indexInfo.Items.Contains(pageInfo), "A modified page must be in the index.");
                }

                // Verify the leaf item infos match the leaf items.
                Assert.True(
                    indexInfo.Items.Count == indexInfo.Index.Items.Count,
                    "The number of page infos must match the number of page items.");
                for (var pageIndex = 0; pageIndex < indexInfo.Items.Count; pageIndex++)
                {
                    var pageInfo = indexInfo.Items[pageIndex];
                    var leafInfos = await pageInfo.GetLeafInfosAsync();
                    var page = await pageInfo.GetPageAsync();
                    Assert.True(
                        page.Items.Count == leafInfos.Count,
                        "The number of leaf items must match the number of leaf item infos.");

                    for (var leafIndex = 0; leafIndex < page.Items.Count; leafIndex++)
                    {
                        var leafInfoVersion = leafInfos[leafIndex].Version;
                        var leafItemVersion = NuGetVersion.Parse(page.Items[leafIndex].CatalogEntry.Version);
                        Assert.True(
                            leafInfoVersion == leafItemVersion,
                            "The list of leaf info versions must match the leaf item versions.");
                    }
                }

                // Verify the modified leaves and deleted leafs are disjoint sets.
                var modifiedVersions = new HashSet<NuGetVersion>(result.ModifiedLeaves.Select(x => x.Version));
                var deletedVersions = new HashSet<NuGetVersion>(result.DeletedLeaves.Select(x => x.Version));
                Assert.True(
                    !modifiedVersions.Overlaps(deletedVersions),
                    "The deleted leaves and modified leaves must be disjoint sets.");

                // Verify the modified leaves are visible in an inlined page or a downloaded page.
                foreach (var leafInfo in result.ModifiedLeaves)
                {
                    foreach (var pageInfo in indexInfo.Items)
                    {
                        if (leafInfo.Version < pageInfo.Lower || leafInfo.Version > pageInfo.Upper)
                        {
                            continue;
                        }

                        Assert.True(
                            pageInfo.IsInlined || pageInfo.IsPageFetched,
                            "A page bounding a modified leaf must either be inlined or the page must be fetched.");

                        var pageLeafInfos = await pageInfo.GetLeafInfosAsync();

                        Assert.True(
                            pageLeafInfos.Any(x => x.Version == leafInfo.Version),
                            "A modified leaf must be in the page bounding its version.");
                    }
                }

                // Verify the deleted leaves are not visible in any of the inlined or downloaded pages.
                foreach (var leafInfo in result.DeletedLeaves)
                {
                    foreach (var pageInfo in indexInfo.Items)
                    {
                        var pageLeafInfos = await pageInfo.GetLeafInfosAsync();

                        Assert.True(
                            !pageLeafInfos.Any(x => x.Version == leafInfo.Version),
                            "A deleted leaf must not be in any page.");
                    }
                }
            }

            private static IEnumerable<NuGetVersion> GetUniqueBounds(PageInfo x)
            {
                yield return x.Lower;

                if (x.Lower != x.Upper)
                {
                    yield return x.Upper;
                }
                else
                {
                    Assert.True(
                        x.Count == 1,
                        "If the upper bound and the lower bound are the same, the leaf item count must be one.");
                }
            }

            private class SimpleOptions<T> : IOptionsSnapshot<T> where T : class, new()
            {
                public SimpleOptions(T value)
                {
                    Value = value;
                }

                public T Value { get; }
                public T Get(string name) => throw new NotImplementedException();
            }

            private class NullLogger<T> : ILogger<T>
            {
                public IDisposable BeginScope<TState>(TState state)
                {
                    return null;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return false;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                }
            }
        }
    }
}
