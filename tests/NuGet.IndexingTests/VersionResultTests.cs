// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Support;
using NuGet.Indexing;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.IndexingTests
{
    public class VersionResultTests
    {
        private readonly Random randomizer = new Random(1000000);

        [Theory]
        [MemberData(nameof(VersionStringSets))]
        public void LegacyResultHasNoSemVer2(string[] versionSet, int expectedNumSemVer1)
        {
            var result = MakeVersionResult(versionSet, randomizeListing: true);

            var legacyResult = result.LegacyVersionDetails;
            Assert.Equal(expectedNumSemVer1, legacyResult.Count());
            foreach (var details in legacyResult)
            {
                Assert.True(!details.IsSemVer2);
            }
        }
        
        [Theory]
        [MemberData(nameof(VersionStringSets))]
        public void SemVer2ResultContainsAllVersion(string[] versionSet, int expectedNumSemVer1)
        {
            var result = MakeVersionResult(versionSet, randomizeListing: true);

            var semVer2Result = result.SemVer2VersionDetails;
            Assert.True(semVer2Result.Count() >= expectedNumSemVer1);
            Assert.True(semVer2Result.Count() == versionSet.Length);
        }

        [Theory]
        [MemberData(nameof(VersionStringSets))]
        public void StableResultContainsNoPrerelVersion(string[] versionSet, int expectedNumSemVer1)
        {
            var result = MakeVersionResult(versionSet, randomizeListing: true);

            var semVer2StableResult = result.GetStableVersions(onlyListed: false, includeSemVer2: true);
            var semVer2StableListedResult = result.GetStableVersions(onlyListed: true, includeSemVer2: true);
            var semVer1StableResult = result.GetStableVersions(onlyListed: false, includeSemVer2: false);
            var semVer1StableListedResult = result.GetStableVersions(onlyListed: true, includeSemVer2: false);

            var semVer2StableCount = 0;
            var semVer2StableListedCount = 0;
            var semVer1StableCount = 0;
            var semVer1StableListedCount = 0;

            foreach (var details in semVer2StableResult)
            {
                var version = NuGetVersion.Parse(details);
                Assert.True(!version.IsPrerelease);
                semVer2StableCount++;
            }

            foreach (var details in semVer2StableListedResult)
            {
                var version = NuGetVersion.Parse(details);
                Assert.True(!version.IsPrerelease);
                semVer2StableListedCount++;
            }

            foreach (var details in semVer1StableResult)
            {
                var version = NuGetVersion.Parse(details);
                Assert.True(!version.IsPrerelease);
                semVer1StableCount++;
            }

            foreach (var details in semVer1StableListedResult)
            {
                var version = NuGetVersion.Parse(details);
                Assert.True(!version.IsPrerelease);
                semVer1StableListedCount++;
            }

            Assert.True(semVer2StableCount >= semVer1StableCount);
            Assert.True(semVer2StableCount >= semVer2StableListedCount);
            Assert.True(semVer1StableCount >= semVer1StableListedCount);
            Assert.True(semVer2StableListedCount >= semVer1StableListedCount);
        }

        [Theory]
        [MemberData(nameof(VersionStringSets))]
        public void ListedOnlyReturnsListed(string[] versionSet, int expectedNumSemVer1)
        {
            var result = MakeVersionResult(versionSet, randomizeListing: true);

            var listedMap = new HashMap<string, VersionDetail>();
            var listedPackages = result.AllVersionDetails.Where(x => x.IsListed);
            foreach(var detail in listedPackages)
            {
                listedMap.Add(detail.FullVersion, detail);
            }

            var semVer2ListedResult = result.GetVersions(onlyListed: true, includeSemVer2: true);
            var semVer1ListedResult = result.GetVersions(onlyListed: true, includeSemVer2: false);

            Assert.True(semVer2ListedResult.Count() <= listedMap.Count);
            foreach(var version in semVer2ListedResult)
            {
                Assert.True(listedMap.ContainsKey(version));
            }

            Assert.True(semVer1ListedResult.Count() <= listedMap.Count);
            Assert.True(semVer1ListedResult.Count() <= semVer2ListedResult.Count());
            foreach (var version in semVer1ListedResult)
            {
                Assert.True(listedMap.ContainsKey(version));
                var versionResult = listedMap[version];
                Assert.True(versionResult.IsListed);
                Assert.True(!versionResult.IsSemVer2);
            }

        }

        private VersionResult MakeVersionResult(string[] versions, bool randomizeListing = false, bool listAll = true)
        {
            var versionResult1 = new VersionResult();
            foreach (var versionString in versions)
            {
                versionResult1.AllVersionDetails.Add(MakeVersionDetail(versionString, randomizeListing ? (randomizer.Next(100) >= 50 ? true : false ) : listAll));
            }

            return versionResult1;
        }

        private VersionDetail MakeVersionDetail(string version, bool listed)
        {
            NuGetVersion parsedVersion;
            if (NuGetVersion.TryParse(version, out parsedVersion))
            {
                return new VersionDetail
                {
                    Downloads = 0,
                    FullVersion = parsedVersion.ToFullString(),
                    NormalizedVersion = parsedVersion.ToNormalizedString(),
                    IsListed = listed,
                    IsStable = !parsedVersion.IsPrerelease,
                    IsSemVer2 = parsedVersion.IsSemVer2
                };
            }

            return new VersionDetail
            {
                Downloads = 0,
                FullVersion = parsedVersion.ToFullString(),
                NormalizedVersion = parsedVersion.ToNormalizedString(),
                IsListed = listed,
                IsStable = !parsedVersion.IsPrerelease,
                IsSemVer2 = parsedVersion.IsSemVer2
            };
        }

        public static IEnumerable<object[]> VersionStringSets
        {
            get
            {
                yield return new object[]
                {
                    // latest semVer1 stable, no semVer2
                    new string[]
                    {
                        "1.0.0"
                    },
                    1
                };

                yield return new object[]
                {
                    // latest semVer1 prerel, no semVer2
                    new string[]
                    {
                        "1.0.0",
                        "2.0.0-prerel"
                    },
                    2
                };

                yield return new object[]
                {
                    // latest SemVer1 stable, including SemVer2
                    new string[]
                    {
                        "1.0.0-pre.3+semVer2",
                        "3.0.0+semVer2",
                        "4.0.0"
                    },
                    1
                };

                yield return new object[]
                {
                    // latest SemVer1 prerel, including SemVer2
                    new string[]
                    {
                        "1.0.0-pre.3+semVer2",
                        "3.0.0+semVer2",
                        "4.0.0",
                        "4.0.6-prerel"
                    },
                    2
                };

                yield return new object[]
                {
                    // latest SemVer2 stable, including SemVer1
                    new string[]
                    {
                        "1.0.0-pre.3+semVer2",
                        "4.0.0",
                        "4.0.6-prerel",
                        "6.0.0+semVer2"
                    },
                    2
                };

                yield return new object[]
                {
                    // latest SemVer2 prerel, including SemVer1
                    new string[]
                    {
                        "1.0.0-pre.3+semVer2",
                        "4.0.0",
                        "4.0.6-prerel",
                        "6.0.0+semVer2",
                        "6.0.5-pre.2+semVer2"
                    },
                    2
                };

                yield return new object[]
                {
                    // latest SemVer2
                    new string[]
                    {
                        "1.0.0-pre.3+semVer2",
                        "6.0.0+semVer2"
                    },
                    0
                };
            }
        }
    }
}
