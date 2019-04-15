// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class VersionListsFacts
    {
        public class Constructor : BaseFacts
        {
            [Fact]
            public void CategorizesVersionsByFilterPredicate()
            {
                var list = Create(
                    _stableSemVer1Listed,
                    _prereleaseSemVer1Listed,
                    _stableSemVer2Listed,
                    _prereleaseSemVer2Listed);

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Equal(
                    new[] { StableSemVer1 },
                    list._versionLists[SearchFilters.Default].GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer1 },
                    list._versionLists[SearchFilters.IncludePrerelease].GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { StableSemVer1, StableSemVer2 },
                    list._versionLists[SearchFilters.IncludeSemVer2].GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer1, StableSemVer2, PrereleaseSemVer2 },
                    list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2].GetLatestVersionInfo().ListedFullVersions);
            }

            [Fact]
            public void AllowsAllEmptyLists()
            {
                var list = Create();

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Empty(list._versionLists[SearchFilters.Default]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrerelease]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludeSemVer2]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2]._versions);
            }

            [Fact]
            public void AllowsSomeEmptyLists()
            {
                var list = Create(_prereleaseSemVer2Listed);

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Empty(list._versionLists[SearchFilters.Default]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrerelease]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludeSemVer2]._versions);
                Assert.Equal(
                    new[] { PrereleaseSemVer2 },
                    list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2].GetLatestVersionInfo().ListedFullVersions);
            }
        }

        public class GetVersionListData : BaseFacts
        {
            [Fact]
            public void ReturnsCurrentSetOfVersions()
            {
                var list = new VersionLists(new VersionListData(new Dictionary<string, VersionPropertiesData>()));

                // add
                list.Upsert(_stableSemVer1Listed);

                // delete
                list.Upsert(_stableSemVer2Listed);
                list.Delete(_stableSemVer2Listed.FullVersion);

                // delete with different case
                list.Upsert(_prereleaseSemVer1Listed);
                list.Delete(_prereleaseSemVer1Listed.FullVersion.ToUpper());

                // unlist with different case
                list.Upsert(_prereleaseSemVer2Listed);
                list.Upsert(new VersionProperties(
                    _prereleaseSemVer2Listed.FullVersion.ToUpper(),
                    new VersionPropertiesData(listed: false, semVer2: true)));

                var data = list.GetVersionListData();
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer2.ToUpper() },
                    data.VersionProperties.Keys.ToArray());
            }
        }

        /// <summary>
        /// This test suite produces every possible initial version state and version list change set to make sure no
        /// exceptions are thrown. This has value given the number of <see cref="Guard.Assert(bool, string)"/> calls in
        /// the implementation.
        /// </summary>
        public class FullEnumerationOfPossibleTestCases
        {
            private readonly ITestOutputHelper _output;

            public FullEnumerationOfPossibleTestCases(ITestOutputHelper output)
            {
                _output = output ?? throw new ArgumentNullException(nameof(output));
            }

            [Fact]
            public async Task AllTestCasesPass()
            {
                // Arrange
                // 4 versions are used since it initially did not find any more bugs than 5 versions and, on most
                // machines will run in under 2 seconds.
                var versions = new[] { "1.0.0", "2.0.0", "3.0.0", "4.0.0" };
                var testCases = new ConcurrentBag<TestCase>(EnumerateTestCases(versions));
                _output.WriteLine($"Running {testCases.Count} test cases.");
                var testResults = new ConcurrentBag<TestResult>();

                // Run the test cases in parallel to improve test duration.
                var tasks = Enumerable
                    .Range(0, 8)
                    .Select(i => Task.Run(() =>
                    {
                        while (testCases.TryTake(out var testCase))
                        {
                            var testResult = ExecuteTestCase(testCase);
                            testResults.Add(testResult);
                        }
                    }));

                // Act
                await Task.WhenAll(tasks);

                // Assert
                _output.WriteLine("Analyzing results.");
                const int maxTestFailuresToOutput = 5;
                var failureCount = 0;
                foreach (var testResult in testResults)
                {
                    if (testResult.Exception == null)
                    {
                        continue;
                    }

                    failureCount++;

                    if (failureCount > maxTestFailuresToOutput)
                    {
                        if (failureCount == maxTestFailuresToOutput + 1)
                        {
                            _output.WriteLine($"{maxTestFailuresToOutput} test failures have been shown. The rest will be hidden.");
                        }

                        continue;
                    }

                    _output.WriteLine("========== TEST CASE FAILURE ==========");
                    _output.WriteLine($"Initial state ({testResult.TestCase.InitialState.Length} versions):");
                    foreach (var version in testResult.TestCase.InitialState)
                    {
                        _output.WriteLine(
                            $" - {version.FullVersion} ({(version.Data.Listed ? "listed" : "unlisted")})");
                    }
                    _output.WriteLine($"Applied changes ({testResult.TestCase.ChangesToApply.Count} changes):");
                    foreach (var version in testResult.TestCase.ChangesToApply)
                    {
                        _output.WriteLine(
                            $" - {(version.IsDelete ? "Delete" : "Upsert")} {version.FullVersion}" +
                            (version.IsDelete ? string.Empty : (version.Data.Listed ? " as listed" : " as unlisted")));
                    }
                    _output.WriteLine("Exception:");
                    _output.WriteLine(testResult.Exception.ToString());
                    _output.WriteLine("=======================================");
                    _output.WriteLine(string.Empty);
                }

                _output.WriteLine($"There were {failureCount} failed test cases.");
                Assert.Equal(0, failureCount);
            }

            private static TestResult ExecuteTestCase(TestCase testCase)
            {
                // Arrange
                var list = ApplyChangesInternal.Create(testCase.InitialState);

                try
                {
                    // Act & Assert
                    var output = list.ApplyChangesInternal(testCase.ChangesToApply);

                    // In a simplistic way, determine the expected version set and their listed status.
                    var expectedVersions = new Dictionary<NuGetVersion, bool>();
                    foreach (var version in testCase.InitialState)
                    {
                        expectedVersions[version.ParsedVersion] = version.Data.Listed;
                    }

                    foreach (var version in testCase.ChangesToApply)
                    {
                        if (version.IsDelete)
                        {
                            expectedVersions.Remove(version.ParsedVersion);
                        }
                        else
                        {
                            expectedVersions[version.ParsedVersion] = version.Data.Listed;
                        }
                    }

                    // Verify it against the version list data.
                    var data = list.GetVersionListData();
                    Assert.Equal(
                        expectedVersions.Keys.Select(x => x.ToFullString()).OrderBy(x => x).ToArray(),
                        data.VersionProperties.Keys.OrderBy(x => x).ToArray());
                    foreach (var pair in expectedVersions)
                    {
                        Assert.True(
                            pair.Value == data.VersionProperties[pair.Key.ToFullString()].Listed,
                            $"{pair.Key.ToFullString()} should have Listed = {pair.Value} but does not.");
                    }

                    // Verify it against the IncludePrereleaseAndSemVer2 version list, since this has all versions.
                    var filteredList = list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2];
                    Assert.Equal(
                        expectedVersions
                            .Where(x => x.Value)
                            .OrderBy(x => x.Key)
                            .Select(x => x.Key.ToFullString())
                            .ToArray(),
                        filteredList.GetLatestVersionInfo()?.ListedFullVersions ?? new string[0]);
                    Assert.Equal(
                        expectedVersions
                            .Where(x => x.Value)
                            .Select(x => x.Key)
                            .OrderBy(x => x)
                            .LastOrDefault()?
                            .ToFullString(),
                        filteredList.GetLatestVersionInfo()?.FullVersion);

                    return new TestResult(testCase, exception: null);
                }
                catch (Exception exception)
                {
                    return new TestResult(testCase, exception);
                }
            }

            private static IEnumerable<TestCase> EnumerateTestCases(
                IReadOnlyList<string> fullVersions)
            {
                // The GetAllVersionListChanges subroutine first finds all subsets of the version set. For each subset
                // of versions, enumerate all combinations of version actions for that subset. For example, consider
                // the subset [ 1.0.0, 2.0.0 ] and the version actions [ Listed (L), Unlisted (U), Deleted (D) ].
                // The combinations (in no particular order) would be:
                //
                //   [
                //     [ L-1.0.0, L-2.0.0 ], [ L-1.0.0, U-2.0.0 ], [ L-1.0.0, D-2.0.0 ],
                //     [ U-1.0.0, L-2.0.0 ], [ U-1.0.0, U-2.0.0 ], [ U-1.0.0, D-2.0.0 ],
                //     [ D-1.0.0, L-2.0.0 ], [ D-1.0.0, U-2.0.0 ], [ D-1.0.0, D-2.0.0 ]
                //   ]
                //
                // The initialState sequence is this full enumeration without the Deleted version action. This is
                // because a package deleted initially will simply not be present. The changesToApply sequence is this
                // full enumeration with all version actions (as in the example).
                //
                // Finally, the cartesian produce of these two sequences is produced. Each pair is a test case.
                var allActions = Enum.GetValues(typeof(VersionAction)).Cast<VersionAction>().ToArray();
                var actionsExceptDelete = allActions.Where(x => x != VersionAction.Deleted).ToArray();

                foreach (var initialState in GetAllVersionListChanges(fullVersions, actionsExceptDelete))
                {
                    foreach (var changeToApply in GetAllVersionListChanges(fullVersions, allActions))
                    {
                        yield return new TestCase(initialState.ToArray(), changeToApply.ToList());
                    }
                }
            }

            private static IEnumerable<IEnumerable<VersionListChange>> GetAllVersionListChanges(
                IReadOnlyCollection<string> fullVersion,
                IReadOnlyList<VersionAction> actions)
            {
                foreach (var versionSubsetSequence in SubsetsOf(fullVersion))
                {
                    var versionSubset = versionSubsetSequence.ToList();
                    var combinations = CombinationsOfTwo(versionSubset, actions);
                    foreach (var combination in combinations)
                    {
                        yield return combination.Select(x => ToVersionListChange(x.Item1, x.Item2));
                    }
                }
            }

            private static VersionListChange ToVersionListChange(string fullVersion, VersionAction action)
            {
                switch (action)
                {
                    case VersionAction.Listed:
                        return VersionListChange.Upsert(
                            fullVersion,
                            new VersionPropertiesData(listed: true, semVer2: false));
                    case VersionAction.Unlisted:
                        return VersionListChange.Upsert(
                            fullVersion,
                            new VersionPropertiesData(listed: false, semVer2: false));
                    case VersionAction.Deleted:
                        return VersionListChange.Delete(NuGetVersion.Parse(fullVersion));
                    default:
                        throw new NotSupportedException($"The version action {action} is not supported.");
                }
            }

            /// <summary>
            /// Source: https://stackoverflow.com/a/3098381
            /// </summary>
            private static IEnumerable<IEnumerable<Tuple<T, int>>> CombinationsOfTwoByIndex<T>(
                IEnumerable<T> sequenceA,
                IEnumerable<int> sequenceBCounts)
            {
                // This takes as input a sequence of elements (A) and a sequence of element counts (related to another
                // sequence B). The count at each position is how many elements from B to combine with that element of
                // A. Suppose the input is:
                //
                //   A = [ x, y, z ]
                //   B = [ 2, 2, 2 ]
                //
                // The output (in no particular order) would be:
                //
                //   [
                //     [ x-1, y-1, z-1 ], [ x-1, y-1, z-2 ],
                //     [ x-1, y-2, z-1 ], [ x-1, y-2, z-2 ],
                //     [ x-2, y-1, z-1 ], [ x-2, y-1, z-2 ],
                //     [ x-2, y-2, z-1 ], [ x-2, y-2, z-2 ],
                //   ]
                //
                // This allows the caller to index into sequence B and produce the combinations of A and B.
                return from cpLine in CartesianProduct(
                       from count in sequenceBCounts select Enumerable.Range(1, count))
                       select cpLine.Zip(sequenceA, (x1, x2) => Tuple.Create(x2, x1));

            }

            private static IEnumerable<IEnumerable<Tuple<T1, T2>>> CombinationsOfTwo<T1, T2>(
                IReadOnlyCollection<T1> sequenceA,
                IReadOnlyList<T2> sequenceB)
            {
                // This has the same behavior as CombinationsOfTwoByIndex but maps the sequence B indexes to actual
                // values. Suppose the input is:
                //
                //   A = [ x, y, z ]
                //   B = [ a, b ]
                //
                // The output (in no particular order) would be:
                //
                //   [
                //     [ x-a, y-a, z-a ], [ x-a, y-a, z-b ],
                //     [ x-a, y-b, z-a ], [ x-a, y-b, z-b ],
                //     [ x-b, y-a, z-a ], [ x-b, y-a, z-b ],
                //     [ x-b, y-b, z-a ], [ x-b, y-b, z-b ],
                //   ]
                //
                // This allows the caller to create combinations of A and B where A is fixed but B is varied per
                // returned combination.
                var arr2 = Enumerable.Repeat(sequenceB.Count, sequenceA.Count);
                var combinations = CombinationsOfTwoByIndex(sequenceA, arr2);
                return combinations.Select(x => x.Select(t => Tuple.Create(t.Item1, sequenceB[t.Item2 - 1])));
            }

            /// <summary>
            /// Source: https://stackoverflow.com/a/3098381
            /// </summary>
            private static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
            {
                IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
                return sequences.Aggregate(
                    emptyProduct,
                    (accumulator, sequence) =>
                        from accseq in accumulator
                        from item in sequence
                        select accseq.Concat(new[] { item })
                    );
            }

            /// <summary>
            /// Source: https://stackoverflow.com/a/999182
            /// </summary>
            private static IEnumerable<IEnumerable<T>> SubsetsOf<T>(IEnumerable<T> source)
            {
                // This produces all subsets of the input. This includes the input itself and the empty set. The term
                // "set" is used to emphasize that order does not matter. The input is assumed to have unique items. If
                // it has duplicates, some output sets will also have duplicates.
                if (!source.Any())
                {
                    return Enumerable.Repeat(Enumerable.Empty<T>(), 1);
                }

                var element = source.Take(1);

                var haveNots = SubsetsOf(source.Skip(1));
                var haves = haveNots.Select(set => element.Concat(set));

                return haves.Concat(haveNots);
            }

            private enum VersionAction
            {
                Listed,
                Unlisted,
                Deleted,
            };

            private class TestCase
            {
                public TestCase(VersionListChange[] initialState, IReadOnlyList<VersionListChange> changesToApply)
                {
                    InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
                    ChangesToApply = changesToApply ?? throw new ArgumentNullException(nameof(changesToApply));
                }

                public VersionListChange[] InitialState { get; }
                public IReadOnlyList<VersionListChange> ChangesToApply { get; }
            }

            private class TestResult
            {
                public TestResult(TestCase testCase, Exception exception)
                {
                    TestCase = testCase;
                    Exception = exception;
                }

                public TestCase TestCase { get; }
                public Exception Exception { get; }
            }
        }

        public class ApplyChanges : BaseFacts
        {
            [Fact]
            public void ProcessesAndSolidifiesChanges()
            {
                var v1 = new Versions("1.0.0");
                var v2 = new Versions("2.0.0");
                var v3 = new Versions("3.0.0");
                var data = new VersionListData(
                    new[] { v1.Listed, v3.Listed }.ToDictionary(x => x.FullVersion, x => x.Data));
                var list = new VersionLists(data);

                var output = list.ApplyChanges(new[] { v2.Listed, v3.Unlisted });

                Assert.Equal(
                    Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>().OrderBy(x => x).ToArray(),
                    output.Search.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { v1.Parsed, v2.Parsed, v3.Parsed },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.False(output.Hijack[v1.Parsed].Delete);
                Assert.False(output.Hijack[v1.Parsed].UpdateMetadata);
                Assert.False(output.Hijack[v1.Parsed].LatestSemVer1);
                Assert.False(output.Hijack[v2.Parsed].Delete);
                Assert.True(output.Hijack[v2.Parsed].UpdateMetadata);
                Assert.True(output.Hijack[v2.Parsed].LatestSemVer1);
                Assert.False(output.Hijack[v3.Parsed].Delete);
                Assert.True(output.Hijack[v3.Parsed].UpdateMetadata);
                Assert.False(output.Hijack[v3.Parsed].LatestSemVer1);
            }
        }

        public class ApplyChangesInternal : BaseFacts
        {
            internal readonly Versions _v1;
            internal readonly Versions _v2;
            internal readonly Versions _v3;
            internal readonly Versions _v4;
            internal readonly Versions _v5;

            public ApplyChangesInternal()
            {
                // Use all stable, SemVer 1.0.0 packages for simplicity. Search filter predicate logic is covered by
                // other tests.
                _v1 = new Versions("1.0.0");
                _v2 = new Versions("2.0.0");
                _v3 = new Versions("3.0.0");
                _v4 = new Versions("4.0.0");
                _v5 = new Versions("5.0.0");
            }

            [Fact]
            public void ProcessesVersionsInDescendingOrder()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v3.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, null, false);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v3, null, true, true);
            }

            [Fact]
            public void InterleavedUpsertsWithNoNewLatest()
            {
                var list = Create(_v1.Listed, _v3.Listed, _v5.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v4.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateVersionList);
                AssertHijackKeys(output, _v2, _v4, _v5);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v4, null, true, false);
                AssertHijack(output, _v5, null, null, true);
            }

            [Fact]
            public void InterleavedUpsertsWithNewLatest()
            {
                var list = Create(_v1.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v4.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v2, _v3, _v4);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v3, null, null, false);
                AssertHijack(output, _v4, null, true, true);
            }

            [Fact]
            public void InterleavedUpsertsWithNewLatestAndUnlistedHighest()
            {
                var list = Create(_v1.Listed, _v3.Listed, _v5.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v4.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v2, _v3, _v4);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v3, null, null, false);
                AssertHijack(output, _v4, null, true, true);
            }

            [Fact]
            public void InterleavedUpsertsWithRelistedLatest()
            {
                var list = Create(_v1.Listed, _v3.Listed, _v5.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v4.Listed, _v5.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v2, _v3, _v4, _v5);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v3, null, null, false);
                AssertHijack(output, _v4, null, true, false);
                AssertHijack(output, _v5, null, true, true);
            }

            [Fact]
            public void RelistNewLatest()
            {
                var list = Create(_v1.Listed, _v2.Unlisted, _v3.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, null, false);
                AssertHijack(output, _v2, null, true, true);
            }

            [Fact]
            public void RelistExistingLatest()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, null, true, true);
            }

            [Fact]
            public void RelistNonLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateVersionList);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, null, true);
            }

            [Fact]
            public void UnlistLastListed()
            {
                var list = Create(_v1.Unlisted, _v2.Listed, _v3.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v2);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void UnlistNonLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Unlisted, _v1.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateVersionList);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v3, null, null, true);
            }

            [Fact]
            public void EmptyChangeList()
            {
                var list = Create(_v1.Listed, _v2.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(Enumerable.Empty<VersionListChange>());

                Assert.Empty(output.SearchChanges);
                Assert.Empty(output.HijackDocuments);
            }

            [Fact]
            public void DeleteNonLatestAndUpsertLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted, _v3.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, true, null, null);
                AssertHijack(output, _v2, null, null, false);
                AssertHijack(output, _v3, null, true, true);
            }

            [Fact]
            public void DeleteLatestAndUpsertHigherLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted, _v3.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v2, _v3);
                AssertHijack(output, _v2, true, null, null);
                AssertHijack(output, _v3, null, true, true);
            }

            [Fact]
            public void DeleteLatestAndUpsertLowerLatest()
            {
                var list = Create(_v1.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v3.Deleted, _v2.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, null, false);
                AssertHijack(output, _v2, null, true, true);
                AssertHijack(output, _v3, true, null, null);
            }

            [Fact]
            public void DeleteLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.DowngradeLatest);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, null, true);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void UnlistLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.DowngradeLatest);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, null, true);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void UnlistLatestAndRelistNewLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, true);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void DeleteLatestAndUpsertNonLatest()
            {
                var list = Create(_v2.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v3.Deleted, _v1.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.DowngradeLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, null, true);
                AssertHijack(output, _v3, true, null, null);
            }

            [Fact]
            public void DeleteNonLatestAndUpsertNonLatest()
            {
                var list = Create(_v2.Listed, _v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted, _v1.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateVersionList);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, true, null, null);
                AssertHijack(output, _v3, null, null, true);
            }

            [Fact]
            public void DeleteLastListedWithOneRemainingUnlisted()
            {
                var list = Create(_v1.Unlisted, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v2);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void DeleteVeryLastWhenLastIsListed()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, true, null, null);
            }

            [Fact]
            public void DeleteVeryLastWhenLastIsUnlisted()
            {
                var list = Create(_v1.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, true, null, null);
            }

            [Fact]
            public void AddSingleFirstWhichIsUnlisted()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, null, true, false);
            }

            [Fact]
            public void AddSingleFirstWhichIsListed()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, null, true, true);
            }

            [Fact]
            public void DeleteLatestAndAndNewLatestWithoutAnyOtherVersions()
            {
                var list = Create(_v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, true);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void DeleteLatestAndAndTwoNewLatestWithoutAnyOtherVersions()
            {
                var list = Create(_v3.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Listed, _v3.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, true, true);
                AssertHijack(output, _v3, true, null, null);
            }

            [Fact]
            public void RejectsMultipleChangesForSameVersion()
            {
                var list = Create();

                var ex = Assert.Throws<ArgumentException>(
                    () => list.ApplyChangesInternal(new[]
                    {
                        _v1.Listed,
                        _v1.Unlisted,
                        _v2.Listed,
                        _v2.Listed,
                        _v2.Listed,
                    }));
                Assert.Contains(
                    "There are multiple changes for the following version(s): 1.0.0 (2 changes), 2.0.0 (3 changes)",
                    ex.Message);
            }

            [Fact]
            public void AddMultipleFirstWhenAllAreListed()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, true, true);
            }

            [Fact]
            public void AddMultipleFirstWhenAllAreUnlisted()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Unlisted, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void AddMultipleFirstWhenWithListedLessThanUnlisted()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, true);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void AddMultipleFirstWhenWithListedGreaterThanUnlisted()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Unlisted, _v2.Listed });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, null, true, true);
            }

            [Fact]
            public void DeleteNonExistingVersionFromEmptyList()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1);
                AssertHijack(output, _v1, true, null, null);
            }

            [Fact]
            public void DeleteNonExistingVersionFromListWithLatest()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateVersionList);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, null, true);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void DeleteNonExistingVersionFromListWithOnlyUnlisted()
            {
                var list = Create(_v1.Unlisted);

                var output = list.ApplyChangesInternal(new[] { _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v2);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void DeleteNonExistingVersionAndAddNewVersion()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v3.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, null, false);
                AssertHijack(output, _v2, null, true, true);
                AssertHijack(output, _v3, true, null, null);
            }

            [Fact]
            public void UnlistLatestAndDeleteNextLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, true, null, null);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void DeleteNonExistentAndUnlistLatest()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Unlisted, _v2.Deleted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, false);
                AssertHijack(output, _v2, true, null, null);
            }

            [Fact]
            public void UnlistNewHighestVersionAndDeleteLatest()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v1.Deleted, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.Delete);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, true, null, null);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void UnlistNewHighestVersionAndUnlistLatest()
            {
                var list = Create(_v1.Listed, _v2.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Unlisted, _v3.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.DowngradeLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, null, true);
                AssertHijack(output, _v2, null, true, false);
                AssertHijack(output, _v2, null, true, false);
            }

            [Fact]
            public void AddUnlistedHighestThenNewLatest()
            {
                var list = Create(_v1.Listed);

                var output = list.ApplyChangesInternal(new[] { _v2.Listed, _v3.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.UpdateLatest);
                AssertHijackKeys(output, _v1, _v2, _v3);
                AssertHijack(output, _v1, null, null, false);
                AssertHijack(output, _v2, null, true, true);
                AssertHijack(output, _v3, null, true, false);
            }
            
            [Fact]
            public void AddUnlistedHighestThenFirstLatest()
            {
                var list = Create();

                var output = list.ApplyChangesInternal(new[] { _v1.Listed, _v2.Unlisted });

                AssertSearchFilters(output, SearchIndexChangeType.AddFirst);
                AssertHijackKeys(output, _v1, _v2);
                AssertHijack(output, _v1, null, true, true);
                AssertHijack(output, _v2, null, true, false);
            }

            private void AssertSearchFilters(MutableIndexChanges output, SearchIndexChangeType type)
            {
                Assert.Equal(type, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(type, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(type, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(type, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
            }

            private void AssertHijackKeys(MutableIndexChanges output, params Versions[] versions)
            {
                Assert.Equal(
                    versions.Select(x => x.Parsed).OrderBy(x => x).ToArray(),
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
            }

            private void AssertHijack(MutableIndexChanges output, Versions versions, bool? delete, bool? updateMetadata, bool? latest)
            {
                Assert.Equal(delete, output.HijackDocuments[versions.Parsed].Delete);
                Assert.Equal(updateMetadata, output.HijackDocuments[versions.Parsed].UpdateMetadata);
                Assert.Equal(latest, output.HijackDocuments[versions.Parsed].LatestStableSemVer1);
                Assert.Equal(latest, output.HijackDocuments[versions.Parsed].LatestSemVer1);
                Assert.Equal(latest, output.HijackDocuments[versions.Parsed].LatestStableSemVer2);
                Assert.Equal(latest, output.HijackDocuments[versions.Parsed].LatestSemVer2);
            }

            internal static VersionLists Create(params VersionListChange[] versions)
            {
                if (versions.Any(x => x.IsDelete))
                {
                    throw new ArgumentException(nameof(versions));
                }

                var data = new VersionListData(versions.ToDictionary(x => x.FullVersion, x => x.Data));
                return new VersionLists(data);
            }
        }

        public class Upsert : BaseFacts
        {
            [Fact]
            public void ReplacesLatestFullVersionByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

                Assert.Equal(
                    new[] { "1.2.0-ALPHA.1+somethingelse" },
                    list.GetVersionListData().VersionProperties.Keys.ToArray());
            }

            [Fact]
            public void ReplacesNonLatestFullVersionByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("2.0.0", new VersionPropertiesData(listed: true, semVer2: true)),
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

                Assert.Equal(
                    new[] { "1.2.0-ALPHA.1+somethingelse", "2.0.0" },
                    list.GetVersionListData().VersionProperties.Keys.ToArray());
            }

            [Fact]
            public void DifferentUpdateLatestForAllFilters()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed, _stableSemVer2Listed, _prereleaseSemVer2Listed);
                var latest = new VersionProperties("5.0.0", new VersionPropertiesData(listed: true, semVer2: false));

                var output = list.Upsert(latest);
                
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[]
                    {
                        _stableSemVer1Listed.ParsedVersion,
                        _prereleaseSemVer1Listed.ParsedVersion,
                        _stableSemVer2Listed.ParsedVersion,
                        _prereleaseSemVer2Listed.ParsedVersion,
                        latest.ParsedVersion,
                    },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: false,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: false,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: false,
                        latestSemVer2: null),
                    output.HijackDocuments[_stableSemVer2Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer2Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.HijackDocuments[latest.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestVersion()
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: false,
                        latestStableSemVer2: true,
                        latestSemVer2: false),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: true,
                        latestStableSemVer2: false,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestUnlistedVersion()
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestVersion()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: false,
                        latestStableSemVer2: true,
                        latestSemVer2: false),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestUnlistedVersion()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: true,
                        latestStableSemVer2: false,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestUnlistedVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_prereleaseSemVer1Unlisted);

                var output = list.Upsert(_stableSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestUnlistedVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_prereleaseSemVer1Unlisted);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenOtherVersionExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenNoOtherVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenOnlyUnlistOtherVersionExists()
            {
                var list = Create(_stableSemVer1Unlisted, _prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);

                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistNonLatestWhenLatestExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }
        }

        public class Delete : BaseFacts
        {
            [Fact]
            public void DeletesByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Delete("1.2.0.0-ALPHA.1+somethingelse");

                Assert.Empty(list.GetVersionListData().VersionProperties);
            }

            [Fact]
            public void DeleteUnknownVersionWhenSingleListedVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Delete(StableSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteUnknownVersionWhenSingleUnlistedVersionExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteLatestVersionWhenSingleListedVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteLatestVersionWhenTwoListedVersionsExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteNonLatestVersionWhenTwoListedVersionsExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Delete(StableSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.SearchChanges[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.SearchChanges[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.HijackDocuments.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.HijackDocuments[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackDocumentChanges(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.HijackDocuments[_prereleaseSemVer1Listed.ParsedVersion]);
            }
        }

        public abstract class BaseFacts
        {
            internal const string StableSemVer1 = "1.0.0";
            internal const string PrereleaseSemVer1 = "2.0.0-alpha";
            internal const string StableSemVer2 = "3.0.0";
            internal const string PrereleaseSemVer2 = "4.0.0-alpha";

            internal readonly VersionProperties _stableSemVer1Listed;
            internal readonly VersionProperties _stableSemVer1Unlisted;
            internal readonly VersionProperties _prereleaseSemVer1Listed;
            internal readonly VersionProperties _prereleaseSemVer1Unlisted;
            internal readonly VersionProperties _stableSemVer2Listed;
            internal readonly VersionProperties _stableSemVer2Unlisted;
            internal readonly VersionProperties _prereleaseSemVer2Listed;
            internal readonly VersionProperties _prereleaseSemVer2Unlisted;

            protected BaseFacts()
            {
                _stableSemVer1Listed = Create(StableSemVer1, true, false);
                _stableSemVer1Unlisted = Create(StableSemVer1, false, false);
                _prereleaseSemVer1Listed = Create(PrereleaseSemVer1, true, false);
                _prereleaseSemVer1Unlisted = Create(PrereleaseSemVer1, false, false);
                _stableSemVer2Listed = Create(StableSemVer2, true, true);
                _stableSemVer2Unlisted = Create(StableSemVer2, false, true);
                _prereleaseSemVer2Listed = Create(PrereleaseSemVer2, true, true);
                _prereleaseSemVer2Unlisted = Create(PrereleaseSemVer2, false, true);
            }

            private VersionProperties Create(string version, bool listed, bool semVer2)
            {
                return new VersionProperties(version, new VersionPropertiesData(listed, semVer2));
            }

            internal VersionLists Create(params VersionProperties[] versions)
            {
                var data = new VersionListData(versions.ToDictionary(x => x.FullVersion, x => x.Data));
                return new VersionLists(data);
            }

            internal class Versions
            {
                public Versions(string fullOrOriginalVersion)
                {
                    Listed = VersionListChange.Upsert(fullOrOriginalVersion, new VersionPropertiesData(listed: true, semVer2: false));
                    Full = Listed.FullVersion;
                    Parsed = Listed.ParsedVersion;
                    Unlisted = VersionListChange.Upsert(fullOrOriginalVersion, new VersionPropertiesData(listed: false, semVer2: false));
                    Deleted = VersionListChange.Delete(Listed.ParsedVersion);
                    Deleted = VersionListChange.Delete(Listed.ParsedVersion);
                }

                public string Full { get; }
                public NuGetVersion Parsed { get; }
                public VersionListChange Listed { get; }
                public VersionListChange Unlisted { get; }
                public VersionListChange Deleted { get; }
            }
        }
    }
}
