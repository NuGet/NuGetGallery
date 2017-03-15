// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class DownloadsByVersionTests
    {
        [Theory]
        [MemberData(nameof(RollingTotalTestData))]
        public void RollingTotalCountTest(Dictionary<string, int> versionDownloads, int expectedTotal)
        {
            var newDownloadsByVersion = new DownloadsByVersion();
            var rollingTotal = 0;
            foreach (var version in versionDownloads)
            {
                newDownloadsByVersion[version.Key] = version.Value;
                rollingTotal += version.Value;
                Assert.Equal(rollingTotal, newDownloadsByVersion.Total);
                Assert.Equal(newDownloadsByVersion[version.Key], version.Value);
            }

            Assert.Equal(expectedTotal, newDownloadsByVersion.Total);
        }

        [Theory]
        [MemberData(nameof(UpdateTotalTestData))]
        public void UpdateTotalTest(Dictionary<string, int> originalData, Dictionary<string, int> updatedVersionDownloads, int expectedTotal)
        {
            var newDownloadsByVersion = new DownloadsByVersion();
            foreach (var entry in originalData)
            {
                newDownloadsByVersion[entry.Key] = entry.Value;
            }

            foreach (var version in updatedVersionDownloads)
            {
                newDownloadsByVersion[version.Key] = version.Value;
            }

            Assert.Equal(expectedTotal, newDownloadsByVersion.Total);
        }

        [Fact]
        public void ReturnZeroForUnknownTest()
        {
            var newDownloadsByVersion = new DownloadsByVersion();
            Assert.Equal(0, newDownloadsByVersion["not.real.version"]);
        }

        public static IEnumerable<object[]> RollingTotalTestData
        {
            get
            {
                // simple
                yield return new object[]
                {
                    new Dictionary<string, int> {
                        { "1.0.0", 10 },
                        { "1.0.1", 10 },
                        { "2.0.0", 20 }
                    },
                    40
                };
            }
        }

        public static IEnumerable<object[]> UpdateTotalTestData
        {
            get
            {
                // simple overwrite
                yield return new object[]
                {
                    new Dictionary<string, int> {
                        { "1.0.0", 10 },
                        { "1.0.1", 10 },
                        { "2.0.0", 20 }
                    },
                    new Dictionary<string, int> {
                        { "2.0.0", 30 }
                    },
                    50
                };

                // overwrite + new
                yield return new object[]
                {
                    new Dictionary<string, int> {
                        { "1.0.0", 10 },
                        { "1.0.1", 10 },
                        { "2.0.0", 20 }
                    },
                    new Dictionary<string, int> {
                        { "2.0.0", 30 },
                        { "3.0.0", 100 }
                    },
                    150
                };

                // overwrite all
                yield return new object[]
                {
                    new Dictionary<string, int> {
                        { "1.0.0", 10 },
                        { "1.0.1", 10 },
                        { "2.0.0", 20 }
                    },
                    new Dictionary<string, int> {
                        { "1.0.0", 100 },
                        { "1.0.1", 100 },
                        { "2.0.0", 100 }
                    },
                    300
                };
            }
        }
    }
}
