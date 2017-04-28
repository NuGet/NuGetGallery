// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lucene.Net.Search;
using Newtonsoft.Json;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using NuGet.Versioning;
using Xunit;

namespace NuGet.IndexingTests
{
    public class ResponseFormatterTests
    {

        [Theory]
        [MemberData(nameof(V2CountResultData))]
        public void WriteV2CountResultTest(int totalHits, string expected)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteV2CountResult(writer, totalHits);

                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(V2ResultData))]
        public void WriteV2ResultTest(
            string indexName,
            int numDocs,
            Dictionary<string, string> commitUserData,
            int topDocsTotalHits,
            float topDocsMaxScore,
            int skip,
            int take,
            string expected)
        {
            var searcher = new MockSearcher(indexName, numDocs, commitUserData, versions: Constants.VersionResults);
            var topDocs = new TopDocs(topDocsTotalHits, Constants.ScoreDocs, topDocsMaxScore);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteV2Result(writer, searcher, topDocs, skip, take, SemVerHelpers.SemVer2Level);

                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(StatsResultData))]
        [MemberData(nameof(DiagAuxiliaryData))]
        public void WriteDiagStatsResultTest(
            string indexName,
            int numDocs,
            Dictionary<string, string> commitUserData,
            Dictionary<string, DateTime?> auxFileData,
            string expected)
        {
            var currentTime = DateTime.UtcNow;
            var searcher = new MockSearcher(indexName, numDocs, commitUserData, versions: null, reloadTime: currentTime, lastModifiedTimeForAuxFiles: auxFileData, machineName: "TestMachineX");

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteStatsResult(writer, searcher);
                var expectedResult = string.Format(
                    expected,
                    searcher.Manager.MachineName.ToString(),
                    searcher.LastReopen.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFK"),
                    searcher.Manager.LastIndexReloadTime.Value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFK"),
                    searcher.Manager.LastAuxiliaryDataLoadTime.Value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFK")
                );

                Assert.Equal(expectedResult, sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(AutoCompleteResultData))]
        public void WriteAutoCompleteResultTest(
            string indexName,
            int numDocs,
            Dictionary<string, string> commitUserData,
            int topDocsTotalHits,
            float topDocsMaxScore,
            int skip,
            int take,
            bool includeExplanation,
            string expected)
        {
            var searcher = new MockSearcher(indexName, numDocs, commitUserData);
            var topDocs = new TopDocs(topDocsTotalHits, Constants.ScoreDocs, topDocsMaxScore);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteAutoCompleteResult(writer, searcher, topDocs, skip, take, includeExplanation, NuGetQuery.MakeQuery("test"));

                Assert.Equal(string.Format(expected,
                    searcher.LastReopen,
                    Constants.MockBase,
                    Constants.LucenePropertyId,
                    Constants.MockExplanationBase), sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(AutoCompleteVersionResultData))]
        public void WriteAutoCompleteVersionResultTest(
            string indexName,
            int numDocs,
            Dictionary<string, string> commitUserData,
            int topDocsTotalHits,
            float topDocsMaxScore,
            bool includePrerelease,
            string expected)
        {
            var searcher = new MockSearcher(indexName, numDocs, commitUserData, versions: Constants.VersionResults);
            var topDocs = new TopDocs(topDocsTotalHits, Constants.ScoreDocs, topDocsMaxScore);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteAutoCompleteVersionResult(writer, searcher, includePrerelease, SemVerHelpers.SemVer2Level, topDocs);

                Assert.Equal(string.Format(expected, searcher.LastReopen), sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(SearchResultData))]
        public void WriteSearchResultTest(
            string indexName,
            int numDocs,
            Dictionary<string, string> commitUserData,
            int topDocsTotalHits,
            float topDocsMaxScore,
            int skip,
            int take,
            bool includePrerelease,
            bool includeExplanation,
            NuGetVersion semVerlLevel,
            string scheme,
            string expectedBaseUrl,
            string expected)
        {
            var searcher = new MockSearcher(indexName, numDocs, commitUserData, versions: Constants.VersionResults);
            var topDocs = new TopDocs(topDocsTotalHits, Constants.ScoreDocs, topDocsMaxScore);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                ResponseFormatter.WriteSearchResult(writer, searcher, scheme, topDocs, skip, take, includePrerelease, includeExplanation, semVerlLevel, NuGetQuery.MakeQuery("test"));

                Assert.Equal(string.Format(expected,
                    expectedBaseUrl,
                    searcher.LastReopen,
                    Constants.MockBase.ToLower(),
                    Constants.LucenePropertyId.ToLower(),
                    Constants.MockBase,
                    Constants.LucenePropertyId,
                    Constants.LucenePropertyVersion,
                    Constants.LucenePropertyDescription,
                    Constants.LucenePropertySummary,
                    Constants.LucenePropertyTitle,
                    Constants.LucenePropertyIconUrl,
                    Constants.LucenePropertyLicenseUrl,
                    Constants.LucenePropertyProjectUrl), sb.ToString());
            }
        }

        public static IEnumerable<object[]> V2CountResultData
        {
            get
            {
                // simple result
                yield return new object[]
                {
                    10,
                    "{\"totalHits\":10}"
                };
            }
        }

        public static IEnumerable<object[]> StatsResultData
        {
            get
            {
                yield return new object[]
                {
                    "mockIndexName",
                    100,
                    new Dictionary<string, string> {
                        { "user1", "value1" },
                        { "user2", "value2" }
                    },
                    null,
                    "{{\"numDocs\":100,\"indexName\":\"mockIndexName\",\"machineName\":\"{0}\",\"lastReopen\":\"{1}\",\"lastIndexReloadTime\":\"{2}\",\"lastIndexReloadDurationInMilliseconds\":-1,\"lastAuxiliaryDataLoadTime\":\"{3}\",\"lastAuxiliaryDataUpdateTime\":{{}},\"CommitUserData\":{{\"user1\":\"value1\",\"user2\":\"value2\"}}}}"
                };

                // no userData
                yield return new object[]
                {
                    "mockNoUser",
                    10,
                    new Dictionary<string, string> {},
                    null,
                    "{{\"numDocs\":10,\"indexName\":\"mockNoUser\",\"machineName\":\"{0}\",\"lastReopen\":\"{1}\",\"lastIndexReloadTime\":\"{2}\",\"lastIndexReloadDurationInMilliseconds\":-1,\"lastAuxiliaryDataLoadTime\":\"{3}\",\"lastAuxiliaryDataUpdateTime\":{{}},\"CommitUserData\":{{}}}}"
                };
            }
        }

        public static IEnumerable<object[]> DiagAuxiliaryData
        {
            get
            {
                // no auxiliary data should return empty object
                yield return new object[]
                {
                    "mockNoUser",
                    10,
                    new Dictionary<string, string> {},
                    new Dictionary<string, DateTime?> {},
                    "{{\"numDocs\":10,\"indexName\":\"mockNoUser\",\"machineName\":\"{0}\",\"lastReopen\":\"{1}\",\"lastIndexReloadTime\":\"{2}\",\"lastIndexReloadDurationInMilliseconds\":-1,\"lastAuxiliaryDataLoadTime\":\"{3}\",\"lastAuxiliaryDataUpdateTime\":{{}},\"CommitUserData\":{{}}}}"
                };

                // missing auxiliary should return null values
                yield return new object[]
                {
                    "mockNoUser",
                    10,
                    new Dictionary<string, string> {},
                    new Dictionary<string, DateTime?> {
                        {"owners.json", null },
                        {"downloads.json", null }
                    },
                    "{{\"numDocs\":10,\"indexName\":\"mockNoUser\",\"machineName\":\"{0}\",\"lastReopen\":\"{1}\",\"lastIndexReloadTime\":\"{2}\",\"lastIndexReloadDurationInMilliseconds\":-1,\"lastAuxiliaryDataLoadTime\":\"{3}\",\"lastAuxiliaryDataUpdateTime\":{{\"owners.json\":null,\"downloads.json\":null}},\"CommitUserData\":{{}}}}"
                };

                // Aux data with timings should be returned correctly.
                var time = DateTime.UtcNow;
                var stringTime = JsonConvert.SerializeObject(time);
                var expectedAuxData = "\"owners.json\":" + stringTime + ",\"downloads.json\":" + stringTime;
                yield return new object[]
                {
                    "mockNoUser",
                    10,
                    new Dictionary<string, string> {},
                    new Dictionary<string, DateTime?> {
                        {"owners.json", time },
                        {"downloads.json", time }
                    },
                    "{{\"numDocs\":10,\"indexName\":\"mockNoUser\",\"machineName\":\"{0}\",\"lastReopen\":\"{1}\",\"lastIndexReloadTime\":\"{2}\",\"lastIndexReloadDurationInMilliseconds\":-1,\"lastAuxiliaryDataLoadTime\":\"{3}\",\"lastAuxiliaryDataUpdateTime\":{{"+ expectedAuxData +"}},\"CommitUserData\":{{}}}}"
                };
            }
        }

        public static IEnumerable<object[]> V2ResultData
        {
            get
            {
                // no timestamp in commitUserData
                yield return new object[]
                {
                    "mockNoTimeStamp",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    0,    // take
                    string.Format("{{\"totalHits\":10,\"index\":\"mockNoTimeStamp\",\"indexTimestamp\":\"{0:G}\",\"data\":[]}}", DateTime.MinValue.ToUniversalTime())
                };

                // timestamp in commitUserData
                yield return new object[]
                {
                    "mockTimeStampInCommitUserData",
                    100,  // Num docs in index
                    new Dictionary<string, string> {
                        { "user1", "value1" },
                        { "user2", "value2" },
                        { "commitTimeStamp", "1/1/2000" }
                    },
                    1,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    0,    // take
                    string.Format("{{\"totalHits\":1,\"index\":\"mockTimeStampInCommitUserData\",\"indexTimestamp\":\"{0:G}\",\"data\":[]}}", new DateTime(2000, 1, 1).ToUniversalTime())
                };

                // timestamp in commitUserData
                yield return new object[]
                {
                    "mockTakeDocuments",
                    100,  // Num docs in index
                    new Dictionary<string, string> {
                        { "user1", "value1" },
                        { "user2", "value2" },
                        { "commitTimeStamp", "1/1/2000" }
                    },
                    2,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    2,    // take
                    string.Format("{{\"totalHits\":2,\"index\":\"mockTakeDocuments\",\"indexTimestamp\":\"{0:G}\",\"data\":[{{\"PackageRegistration\":{{\"{2}\":\"{1}{2}0\",\"DownloadCount\":0,\"Owners\":[]}},\"NormalizedVersion\":\"{1}{3}0\",\"{4}\":\"{1}{4}0\",\"{5}\":\"{1}{5}0\",\"{6}\":\"{1}{6}0\",\"{7}\":\"{1}{7}0\",\"{8}\":\"{1}{8}0\",\"IsLatestStable\":false,\"IsLatest\":false,\"Listed\":true,\"DownloadCount\":0,\"Dependencies\":[],\"SupportedFrameworks\":[],\"PackageFileSize\":0,\"{9}\":\"{1}{9}0\",\"RequiresLicenseAcceptance\":false}},{{\"PackageRegistration\":{{\"{2}\":\"{1}{2}1\",\"DownloadCount\":0,\"Owners\":[]}},\"NormalizedVersion\":\"{1}{3}1\",\"{4}\":\"{1}{4}1\",\"{5}\":\"{1}{5}1\",\"{6}\":\"{1}{6}1\",\"{7}\":\"{1}{7}1\",\"{8}\":\"{1}{8}1\",\"IsLatestStable\":false,\"IsLatest\":false,\"Listed\":true,\"DownloadCount\":0,\"Dependencies\":[],\"SupportedFrameworks\":[],\"PackageFileSize\":0,\"{9}\":\"{1}{9}1\",\"RequiresLicenseAcceptance\":false}}]}}",
                    new DateTime(2000, 1, 1).ToUniversalTime(),
                    Constants.MockBase,
                    Constants.LucenePropertyId,
                    Constants.LucenePropertyVersion,
                    Constants.LucenePropertyTitle,
                    Constants.LucenePropertyDescription,
                    Constants.LucenePropertySummary,
                    Constants.LucenePropertyProjectUrl,
                    Constants.LucenePropertyIconUrl,
                    Constants.LucenePropertyLicenseUrl)
                };

                // timestamp in commitUserData
                yield return new object[]
                {
                    "mockSkipAllDocs",
                    100,  // Num docs in index
                    new Dictionary<string, string> {
                        { "user1", "value1" },
                        { "user2", "value2" },
                        { "commitTimeStamp", "1/1/2000" }
                    },
                    2,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    2,    // skip
                    0,    // take
                    string.Format("{{\"totalHits\":2,\"index\":\"mockSkipAllDocs\",\"indexTimestamp\":\"{0:G}\",\"data\":[]}}", new DateTime(2000, 1, 1).ToUniversalTime())
                };
            }
        }

        public static IEnumerable<object[]> AutoCompleteResultData
        {
            get
            {
                // no explanations no docs
                yield return new object[]
                {
                    "mockNoExplanation",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    0,    // take
                    false,// Include Explanation
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":10,\"lastReopen\":\"{0:o}\",\"index\":\"mockNoExplanation\",\"data\":[]}}"
                };

                // include Explanations
                yield return new object[]
                {
                    "mockIncludeExplanations",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    1,    // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    2,    // take
                    true, // Include Explanation
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":1,\"lastReopen\":\"{0:o}\",\"index\":\"mockIncludeExplanations\",\"data\":[\"{1}{2}0\",\"{1}{2}1\"],\"explanations\":[\"1 = {3}\\n\",\"1 = {3}\\n\"]}}"
                };

                // no explanations skip all docs
                yield return new object[]
                {
                    "mockNoExplanation",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    10,    // skip
                    0,    // take
                    false,// Include Explanation
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":10,\"lastReopen\":\"{0:o}\",\"index\":\"mockNoExplanation\",\"data\":[]}}"
                };
            }
        }

        public static IEnumerable<object[]> AutoCompleteVersionResultData
        {
            get
            {
                // no Prerelease
                yield return new object[]
                {
                    "mockNoPrerelease",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    false,// Include Prerelease
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":10,\"lastReopen\":\"{0:o}\",\"index\":\"mockNoPrerelease\",\"data\":[]}}"
                };

                // no prerelease
                yield return new object[]
                {
                    "mockIncludePrerelease",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    1,    // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    true, // Include Prerelease
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":1,\"lastReopen\":\"{0:o}\",\"index\":\"mockIncludePrerelease\",\"data\":[]}}"
                };
            }
        }

        public static IEnumerable<object[]> SearchResultData
        {
            get
            {
                // no results, exclude explanation, exclude prerelease
                yield return new object[]
                {
                    "mockNoResults",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    0,    // take
                    false,// Include Explanation
                    false,// Include Prerelease
                    SemVerHelpers.SemVer2Level,
                    Constants.SchemeNameHttp,
                    Constants.BaseUriSemVer2Http,
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\",\"@base\":\"{0}\"}},\"totalHits\":10,\"lastReopen\":\"{1:o}\",\"index\":\"mockNoResults\",\"data\":[]}}"
                };

                yield return new object[]
                {
                    "mockNoResults",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    0,    // take
                    false,// Include Explanation
                    false,// Include Prerelease
                    SemVerHelpers.SemVer2Level,
                    Constants.SchemeNameHttps,
                    Constants.BaseUriSemVer2Https,
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\",\"@base\":\"{0}\"}},\"totalHits\":10,\"lastReopen\":\"{1:o}\",\"index\":\"mockNoResults\",\"data\":[]}}"
                };

                // include Explanations
                yield return new object[]
                {
                    "mockIncludeExplanations",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    1,    // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    0,    // skip
                    2,    // take
                    true, // Include Explanation
                    false,// Include Prerelease
                    SemVerHelpers.SemVer1Level,
                    Constants.SchemeNameHttp,
                    Constants.BaseUriHttp,
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\",\"@base\":\"{0}\"}},\"totalHits\":1,\"lastReopen\":\"{1:o}\",\"index\":\"{2}IncludeExplanations\",\"data\":[{{\"@id\":\"{0}{2}{3}0/index.json\",\"@type\":\"Package\",\"registration\":\"{0}{2}{3}0/index.json\",\"id\":\"{4}{5}0\",\"version\":\"{4}{6}0\",\"description\":\"{4}{7}0\",\"summary\":\"{4}{8}0\",\"title\":\"{4}{9}0\",\"iconUrl\":\"{4}{10}0\",\"licenseUrl\":\"{4}{11}0\",\"projectUrl\":\"{4}{12}0\",\"totalDownloads\":0,\"versions\":[]}},{{\"@id\":\"{0}{2}{3}1/index.json\",\"@type\":\"Package\",\"registration\":\"{0}{2}{3}1/index.json\",\"id\":\"{4}{5}1\",\"version\":\"{4}{6}1\",\"description\":\"{4}{7}1\",\"summary\":\"{4}{8}1\",\"title\":\"{4}{9}1\",\"iconUrl\":\"{4}{10}1\",\"licenseUrl\":\"{4}{11}1\",\"projectUrl\":\"{4}{12}1\",\"totalDownloads\":0,\"versions\":[]}}]}}"
                };

                // no explanations skip all docs
                yield return new object[]
                {
                    "mockNoExplanation",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    10,    // skip
                    0,    // take
                    false,// Include Explanation
                    false,// Include Prerelease
                    SemVerHelpers.SemVer1Level,
                    Constants.SchemeNameHttps,
                    Constants.BaseUriHttps,
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\",\"@base\":\"{0}\"}},\"totalHits\":10,\"lastReopen\":\"{1:o}\",\"index\":\"mockNoExplanation\",\"data\":[]}}"
                };
            }
        }

        public static IEnumerable<object[]> FindResultData
        {
            get
            {
                // no commitUserData
                yield return new object[]
                {
                    "mockNoTimeStamp",
                    100,  // Num docs in index
                    new Dictionary<string, string> { },
                    10,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":10,\"lastReopen\":\"{0:o}\",\"index\":\"mockNoTimeStamp\",\"registration\":\"{1}{2}{3}0/index.json\"}}"
                };

                // Things and timestamp in commitUserData
                yield return new object[]
                {
                    "mockTimeStampInCommitUserData",
                    100,  // Num docs in index
                    new Dictionary<string, string> {
                        { "user1", "value1" },
                        { "user2", "value2" },
                        { "commitTimeStamp", "1/1/2000" }
                    },
                    1,   // Top Docs Total Hits
                    1.0,  // Top Docs Max Score
                    "{{\"@context\":{{\"@vocab\":\"http://schema.nuget.org/schema#\"}},\"totalHits\":1,\"lastReopen\":\"{0:o}\",\"index\":\"mockTimeStampInCommitUserData\",\"registration\":\"{1}{2}{3}0/index.json\"}}"
                };
            }
        }

        private static Ranking[] GenerateRankings(int numRankings, string idPrefix = "testId")
        {
            var rankingsList = new List<Ranking>();
            for (var i = 0; i < numRankings; i++)
            {
                var ranking = new Ranking();
                ranking.Id = idPrefix + i;
                ranking.Rank = i;

                rankingsList.Add(ranking);
            }

            return rankingsList.ToArray();
        }
    }
}
