// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Newtonsoft.Json;
using NuGet.Versioning;
using LuceneMetadataConstants = NuGet.Indexing.MetadataConstants.LuceneMetadata;

namespace NuGet.Indexing
{
    public static class ResponseFormatter
    {
        // V3 implementation - called directly from the integrated Visual Studio client
        public static void WriteSearchResult(
            JsonWriter jsonWriter,
            NuGetIndexSearcher searcher,
            string scheme,
            TopDocs topDocs,
            int skip,
            int take,
            bool includePrerelease,
            bool includeExplanation,
            NuGetVersion semVerLevel,
            Query query)
        {
            Uri baseAddress;
            var useSemVer2Registration = SemVerHelpers.ShouldIncludeSemVer2Results(semVerLevel);
            RegistrationBaseAddresses registrationAddresses = searcher.Manager.RegistrationBaseAddresses;

            switch (scheme)
            {
                case "https":
                    baseAddress = (useSemVer2Registration ? registrationAddresses.SemVer2Https : registrationAddresses.LegacyHttps);
                    break;
                case "http":
                default:
                    // if scheme specified is invalid, fall back to using as much information as we can
                    baseAddress = (useSemVer2Registration ? registrationAddresses.SemVer2Http : registrationAddresses.LegacyHttp);
                    break;
            }

            jsonWriter.WriteStartObject();
            WriteInfo(jsonWriter, baseAddress, searcher, topDocs);
            WriteData(jsonWriter, searcher, topDocs, skip, take, baseAddress, includePrerelease, includeExplanation, semVerLevel, query);
            jsonWriter.WriteEndObject();
        }

        private static void WriteInfo(JsonWriter jsonWriter, Uri baseAddress, NuGetIndexSearcher searcher, TopDocs topDocs)
        {
            WriteContext(jsonWriter, baseAddress);
            WriteProperty(jsonWriter, "totalHits", topDocs.TotalHits);
            WriteProperty(jsonWriter, "lastReopen", searcher.LastReopen.ToString("o"));
            WriteProperty(jsonWriter, "index", searcher.Manager.IndexName);
        }

        private static void WriteContext(JsonWriter jsonWriter, Uri baseAddress)
        {
            jsonWriter.WritePropertyName("@context");

            jsonWriter.WriteStartObject();
            WriteProperty(jsonWriter, "@vocab", "http://schema.nuget.org/schema#");

            if (baseAddress != null)
            {
                WriteProperty(jsonWriter, "@base", baseAddress.AbsoluteUri);
            }

            jsonWriter.WriteEndObject();
        }

        private static void WriteData(
            JsonWriter jsonWriter, 
            NuGetIndexSearcher searcher, 
            TopDocs topDocs,
            int skip,
            int take,
            Uri baseAddress,
            bool includePrerelease,
            bool includeExplanation,
            NuGetVersion semVerLevel,
            Query query)
        {
            jsonWriter.WritePropertyName("data");

            jsonWriter.WriteStartArray();

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document document = searcher.Doc(scoreDoc.Doc);

                jsonWriter.WriteStartObject();

                string id = document.Get("Id");

                var relativeAddress = UriFormatter.MakeRegistrationRelativeAddress(id);
                var absoluteAddress = new Uri(baseAddress, relativeAddress).AbsoluteUri;

                WriteProperty(jsonWriter, "@id", absoluteAddress);
                WriteProperty(jsonWriter, "@type", "Package");
                WriteProperty(jsonWriter, "registration", absoluteAddress);

                WriteProperty(jsonWriter, "id", id);

                WriteDocumentValue(jsonWriter, "version", document, LuceneMetadataConstants.FullVersionPropertyName);
                WriteDocumentValue(jsonWriter, "description", document, "Description");
                WriteDocumentValue(jsonWriter, "summary", document, "Summary");
                WriteDocumentValue(jsonWriter, "title", document, "Title");
                WriteDocumentValue(jsonWriter, "iconUrl", document, "IconUrl");
                WriteDocumentValue(jsonWriter, "licenseUrl", document, "LicenseUrl");
                WriteDocumentValue(jsonWriter, "projectUrl", document, "ProjectUrl");
                WriteDocumentValueAsArray(jsonWriter, "tags", document, "Tags");
                WriteDocumentValueAsArray(jsonWriter, "authors", document, "Authors", true);
                WriteProperty(jsonWriter, "totalDownloads", searcher.Versions[scoreDoc.Doc].AllVersionDetails.Select(item => item.Downloads).Sum());
                WriteVersions(jsonWriter, baseAddress, id, includePrerelease, semVerLevel, searcher.Versions[scoreDoc.Doc]);

                if (includeExplanation)
                {
                    Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
                    WriteProperty(jsonWriter, "explanation", explanation.ToString());
                    WriteProperty(jsonWriter, "score", scoreDoc.Score);
                }

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        private static void WriteVersions(JsonWriter jsonWriter,
            Uri baseAddress,
            string id,
            bool includePrerelease,
            NuGetVersion semVerLevel,
            VersionResult versionResult)
        {
            var includeSemVer2 = SemVerHelpers.ShouldIncludeSemVer2Results(semVerLevel);

            jsonWriter.WritePropertyName("versions");

            jsonWriter.WriteStartArray();

            var results = includePrerelease
                ? (includeSemVer2 ? versionResult.SemVer2VersionDetails : versionResult.LegacyVersionDetails)
                : (includeSemVer2 ? versionResult.StableSemVer2VersionDetails : versionResult.StableLegacyVersionDetails);

            foreach (var item in results.Where(r => r.IsListed))
            {
                var relativeAddress = UriFormatter.MakePackageRelativeAddress(id, item.NormalizedVersion);
                var absoluteAddress = new Uri(baseAddress, relativeAddress).AbsoluteUri;

                jsonWriter.WriteStartObject();
                WriteProperty(jsonWriter, "version", item.FullVersion);
                WriteProperty(jsonWriter, "downloads", item.Downloads);
                WriteProperty(jsonWriter, "@id", absoluteAddress);

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        // V3 auto-complete implementation - called from the Visual Studio "project.json editor"
        public static void WriteAutoCompleteResult(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, bool includeExplanation, Query query)
        {
            jsonWriter.WriteStartObject();
            WriteInfo(jsonWriter, null, searcher, topDocs);
            WriteIds(jsonWriter, searcher, topDocs, skip, take);

            if (includeExplanation)
            {
                WriteExplanations(jsonWriter, searcher, topDocs, skip, take, query);
            }

            jsonWriter.WriteEndObject();
        }

        public static void WriteAutoCompleteVersionResult(JsonWriter jsonWriter, NuGetIndexSearcher searcher, bool includePrerelease, NuGetVersion semVerLevel, TopDocs topDocs)
        {
            jsonWriter.WriteStartObject();
            WriteInfo(jsonWriter, null, searcher, topDocs);
            WriteVersions(jsonWriter, searcher, includePrerelease, semVerLevel, topDocs);
            jsonWriter.WriteEndObject();
        }

        private static void WriteIds(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take)
        {
            jsonWriter.WritePropertyName("data");
            jsonWriter.WriteStartArray();
            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                Document document = searcher.Doc(scoreDoc.Doc);
                string id = document.Get("Id");
                jsonWriter.WriteValue(id);
            }
            jsonWriter.WriteEndArray();
        }

        private static void WriteExplanations(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, Query query)
        {
            jsonWriter.WritePropertyName("explanations");
            jsonWriter.WriteStartArray();
            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
                jsonWriter.WriteValue(explanation.ToString());
            }
            jsonWriter.WriteEndArray();
        }

        private static void WriteVersions(JsonWriter jsonWriter, NuGetIndexSearcher searcher, bool includePrerelease, NuGetVersion semVerLevel, TopDocs topDocs)
        {
            var includeSemVer2 = SemVerHelpers.ShouldIncludeSemVer2Results(semVerLevel);

            jsonWriter.WritePropertyName("data");
            jsonWriter.WriteStartArray();

            if (topDocs.TotalHits > 0)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[0];

                var versions = includePrerelease
                    ? searcher.Versions[scoreDoc.Doc].GetVersions(onlyListed: true, includeSemVer2: includeSemVer2)
                    : searcher.Versions[scoreDoc.Doc].GetStableVersions(onlyListed: true, includeSemVer2: includeSemVer2);

                foreach (var version in versions)
                {
                    jsonWriter.WriteValue(version);
                }
            }

            jsonWriter.WriteEndArray();
        }

        // V2 search implementation - called from the NuGet Gallery
        public static void WriteV2Result(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, NuGetVersion semVerLevel)
        {
            jsonWriter.WriteStartObject();
            WriteInfoV2(jsonWriter, searcher, topDocs);
            WriteDataV2(jsonWriter, searcher, topDocs, skip, take, semVerLevel);
            jsonWriter.WriteEndObject();
        }

        public static void WriteV2CountResult(JsonWriter jsonWriter, int totalHits)
        {
            jsonWriter.WriteStartObject();
            WriteProperty(jsonWriter, "totalHits", totalHits);
            jsonWriter.WriteEndObject();
        }

        private static void WriteInfoV2(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs)
        {
            WriteProperty(jsonWriter, "totalHits", topDocs.TotalHits);

            string timestamp;
            DateTime dt;
            if (searcher.CommitUserData.TryGetValue("commitTimeStamp", out timestamp) &&
                DateTime.TryParse(timestamp, out dt))
            {
                timestamp = dt.ToUniversalTime().ToString("G");
            }
            else
            {
                timestamp = DateTime.MinValue.ToUniversalTime().ToString("G");
            }

            // TODO: can we find this value?
            // WriteProperty(jsonWriter, "timeTakenInMs", 0);
            WriteProperty(jsonWriter, "index", searcher.Manager.IndexName);

            // CommittimeStamp format: 2015-10-12T18:39:39.6830871Z
            // Time format in V2: 10/22/2015 4:53:25 PM
            WriteProperty(jsonWriter, "indexTimestamp", timestamp);
        }

        private static void WriteRegistrationV2(JsonWriter jsonWriter, string id, int downloadCount, IEnumerable<string> owners)
        {
            jsonWriter.WritePropertyName("PackageRegistration");
            jsonWriter.WriteStartObject();

            WriteProperty(jsonWriter, "Id", id);
            WriteProperty(jsonWriter, "DownloadCount", downloadCount);

            jsonWriter.WritePropertyName("Owners");
            jsonWriter.WriteStartArray();

            foreach (string owner in owners)
            {
                jsonWriter.WriteValue(owner);
            }

            jsonWriter.WriteEndArray();

            jsonWriter.WriteEndObject();
        }

        private static void WriteDataV2(JsonWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, NuGetVersion semVerLevel)
        {
            jsonWriter.WritePropertyName("data");

            jsonWriter.WriteStartArray();

            var includeSemVer2 = SemVerHelpers.ShouldIncludeSemVer2Results(semVerLevel);

            var isLatestBitSet = includeSemVer2 ? searcher.LatestSemVer2BitSet : searcher.LatestBitSet;
            var isLatestStableBitSet = includeSemVer2 ? searcher.LatestStableSemVer2BitSet : searcher.LatestStableBitSet;

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                Document document = searcher.Doc(scoreDoc.Doc);

                string id = document.Get("Id");
                string normalizedVersion = document.Get(LuceneMetadataConstants.NormalizedVersionPropertyName);
                string fullVersion = document.Get(LuceneMetadataConstants.FullVersionPropertyName);

                Tuple<int, int> downloadCounts = NuGetIndexSearcher.DownloadCounts(searcher.Versions[scoreDoc.Doc], normalizedVersion);

                bool isLatest = isLatestBitSet.Get(scoreDoc.Doc);
                bool isLatestStable = isLatestStableBitSet.Get(scoreDoc.Doc);

                jsonWriter.WriteStartObject();
                WriteRegistrationV2(jsonWriter, id, downloadCounts.Item1, NuGetIndexSearcher.GetOwners(searcher, id));
                WriteDocumentValue(jsonWriter, "Version", document, LuceneMetadataConstants.VerbatimVersionPropertyName);
                WriteProperty(jsonWriter, "NormalizedVersion", normalizedVersion);
                WriteDocumentValue(jsonWriter, "Title", document, "Title");
                WriteDocumentValue(jsonWriter, "Description", document, "Description");
                WriteDocumentValue(jsonWriter, "Summary", document, "Summary");
                WriteDocumentValue(jsonWriter, "Authors", document, "Authors");
                WriteDocumentValue(jsonWriter, "Copyright", document, "Copyright");
                WriteDocumentValue(jsonWriter, "Language", document, "Language");
                WriteDocumentValue(jsonWriter, "Tags", document, "Tags");
                WriteDocumentValue(jsonWriter, "ReleaseNotes", document, "ReleaseNotes");
                WriteDocumentValue(jsonWriter, "ProjectUrl", document, "ProjectUrl");
                WriteDocumentValue(jsonWriter, "IconUrl", document, "IconUrl");
                WriteProperty(jsonWriter, "IsLatestStable", isLatestStable);
                WriteProperty(jsonWriter, "IsLatest", isLatest);
                WriteProperty(jsonWriter, "Listed", bool.Parse(document.Get("Listed") ?? "true"));
                WriteDocumentValue(jsonWriter, "Created", document, "OriginalCreated");
                WriteDocumentValue(jsonWriter, "Published", document, "OriginalPublished");
                WriteDocumentValue(jsonWriter, "LastUpdated", document, "OriginalPublished");
                WriteDocumentValue(jsonWriter, "LastEdited", document, "OriginalLastEdited");
                WriteProperty(jsonWriter, "DownloadCount", downloadCounts.Item2);
                WriteDocumentValue(jsonWriter, "FlattenedDependencies", document, "FlattenedDependencies");
                jsonWriter.WritePropertyName("Dependencies");
                jsonWriter.WriteRawValue(document.Get("Dependencies") ?? "[]");
                jsonWriter.WritePropertyName("SupportedFrameworks");
                jsonWriter.WriteRawValue(document.Get("SupportedFrameworks") ?? "[]");
                WriteDocumentValue(jsonWriter, "MinClientVersion", document, "MinClientVersion");
                WriteDocumentValue(jsonWriter, "Hash", document, "PackageHash");
                WriteDocumentValue(jsonWriter, "HashAlgorithm", document, "PackageHashAlgorithm");
                WriteProperty(jsonWriter, "PackageFileSize", int.Parse(document.Get("PackageSize") ?? "0"));
                WriteDocumentValue(jsonWriter, "LicenseUrl", document, "LicenseUrl");
                WriteProperty(jsonWriter, "RequiresLicenseAcceptance", bool.Parse(document.Get("RequiresLicenseAcceptance") ?? "false"));
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        // Diagnostic responses
        public static void WriteStatsResult(JsonWriter jsonWriter, NuGetIndexSearcher searcher)
        {
            jsonWriter.WriteStartObject();
            WriteProperty(jsonWriter, "numDocs", searcher.IndexReader.NumDocs());
            WriteProperty(jsonWriter, "indexName", searcher.Manager.IndexName);
            WriteProperty(jsonWriter, "machineName", searcher.Manager.MachineName);
            WriteProperty(jsonWriter, "lastReopen", searcher.LastReopen);
            WriteProperty(jsonWriter, "lastIndexReloadTime", searcher.Manager.LastIndexReloadTime);
            WriteProperty(jsonWriter, "lastIndexReloadDurationInMilliseconds", searcher.Manager.LastIndexReloadDurationInMilliseconds);
            WriteProperty(jsonWriter, "lastAuxiliaryDataLoadTime", searcher.Manager.LastAuxiliaryDataLoadTime);

            jsonWriter.WritePropertyName("lastAuxiliaryDataUpdateTime");
            jsonWriter.WriteStartObject();
            foreach (var userData in searcher.Manager.AuxiliaryFiles?.LastModifiedTimeForFiles ?? new Dictionary<string, DateTime?>())
            {
                WriteProperty(jsonWriter, userData.Key, userData.Value);
            }
            jsonWriter.WriteEndObject();

            jsonWriter.WritePropertyName("CommitUserData");
            jsonWriter.WriteStartObject();
            foreach (var userData in searcher.CommitUserData)
            {
                WriteProperty(jsonWriter, userData.Key, userData.Value);
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }

        // various generic helpers for building JSON results from Lucene Documents
        private static void WriteProperty<T>(JsonWriter jsonWriter, string propertyName, T value)
        {
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteValue(value);
        }

        private static void WriteDocumentValue(JsonWriter jsonWriter, string propertyName, Document document, string fieldName)
        {
            string value = document.Get(fieldName);
            if (value != null)
            {
                WriteProperty(jsonWriter, propertyName, value);
            }
        }

        private static void WriteDocumentValueAsArray(JsonWriter jsonWriter, string propertyName, Document document, string fieldName, bool singleElement = false)
        {
            string value = document.Get(fieldName);
            if (value != null)
            {
                jsonWriter.WritePropertyName(propertyName);
                jsonWriter.WriteStartArray();

                if (singleElement)
                {
                    jsonWriter.WriteValue(value);
                }
                else
                {
                    foreach (var s in value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        jsonWriter.WriteValue(s);
                    }
                }

                jsonWriter.WriteEndArray();
            }
        }
    }
}
