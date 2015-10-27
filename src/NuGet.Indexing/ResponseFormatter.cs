// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Search;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace NuGet.Indexing
{
    public static class ResponseFormatter
    {
        /*
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // V3 implementation - called directly from the integrated Visual Studio client
        //
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        */

        public static string MakeResult(NuGetIndexSearcher searcher, string scheme, TopDocs topDocs, int skip, int take, bool includePrerelease, bool includeExplanation, Query query)
        {
            Uri baseAddress = searcher.Manager.RegistrationBaseAddress[scheme];

            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteInfo(jsonWriter, baseAddress, searcher, topDocs);
                    WriteData(jsonWriter, searcher, topDocs, skip, take, baseAddress, includePrerelease, includeExplanation, query);
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        static void WriteInfo(JsonTextWriter jsonWriter, Uri baseAddress, NuGetIndexSearcher searcher, TopDocs topDocs)
        {
            WriteContext(jsonWriter, baseAddress);
            WriteProperty(jsonWriter, "totalHits", topDocs.TotalHits);
            WriteProperty(jsonWriter, "lastReopen", searcher.LastReopen.ToString("o"));
            WriteProperty(jsonWriter, "index", searcher.Manager.IndexName);
        }

        static void WriteContext(JsonTextWriter jsonWriter, Uri baseAddress)
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

        static void WriteData(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, Uri baseAddress, bool includePrerelease, bool includeExplanation, Query query)
        {
            jsonWriter.WritePropertyName("data");

            jsonWriter.WriteStartArray();

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document document = searcher.Doc(scoreDoc.Doc);               

                jsonWriter.WriteStartObject();

                string url = document.Get("Url");
                string id = document.Get("Id");

                string relativeAddress = UriFormatter.MakeRegistrationRelativeAddress(id);

                WriteProperty(jsonWriter, "@id", relativeAddress);
                WriteProperty(jsonWriter, "@type", "Package");
                WriteProperty(jsonWriter, "registration", new Uri(baseAddress, relativeAddress).AbsoluteUri);

                WriteProperty(jsonWriter, "id", id);

                WriteDocumentValue(jsonWriter, "version", document, "Version");
                WriteDocumentValue(jsonWriter, "domain", document, "Domain");
                WriteDocumentValue(jsonWriter, "description", document, "Description");
                WriteDocumentValue(jsonWriter, "summary", document, "Summary");
                WriteDocumentValue(jsonWriter, "title", document, "Title");
                WriteDocumentValue(jsonWriter, "iconUrl", document, "IconUrl");
                WriteDocumentValue(jsonWriter, "licenseUrl", document, "LicenseUrl");
                WriteDocumentValue(jsonWriter, "projectUrl", document, "ProjectUrl");
                WriteDocumentValueAsArray(jsonWriter, "tags", document, "Tags");
                WriteDocumentValue(jsonWriter, "authors", document, "Authors");
                WriteProperty(jsonWriter, "totalDownloads", searcher.Versions[scoreDoc.Doc].VersionDetails.Select(item => item.Downloads).Sum());
                WriteVersions(jsonWriter, id, includePrerelease, searcher.Versions[scoreDoc.Doc]);

                if (includeExplanation)
                {
                    Explanation explanation = searcher.Explain(query, scoreDoc.Doc);
                    WriteProperty(jsonWriter, "explanation", explanation.ToString());
                }

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        static void WriteVersions(JsonTextWriter jsonWriter, string id, bool includePrerelease, VersionsHandler.VersionResult versionResult)
        {
            jsonWriter.WritePropertyName("versions");

            jsonWriter.WriteStartArray();

            foreach (var item in includePrerelease ? versionResult.VersionDetails : versionResult.StableVersionDetails)
            {
                jsonWriter.WriteStartObject();

                WriteProperty(jsonWriter, "version", item.Version);
                WriteProperty(jsonWriter, "downloads", item.Downloads);
                WriteProperty(jsonWriter, "@id", UriFormatter.MakePackageRelativeAddress(id, item.Version));

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // V3 auto-complete implementation - called from the Visual Studio "project.json editor"
        //
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static string AutoCompleteMakeResult(NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, bool includeExplanation, Query query)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteInfo(jsonWriter, null, searcher, topDocs);
                    WriteIds(jsonWriter, searcher, topDocs, skip, take);
                    
                    if (includeExplanation)
                    {
                        WriteExplanations(jsonWriter, searcher, topDocs, skip, take, query);
                    }
                    
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        static void WriteIds(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take)
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

        static void WriteExplanations(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, Query query)
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

        public static string AutoCompleteMakeVersionResult(NuGetIndexSearcher searcher, bool includePrerelease, TopDocs topDocs)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteInfo(jsonWriter, null, searcher, topDocs);
                    WriteVersions(jsonWriter, searcher, includePrerelease, topDocs);
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        static void WriteVersions(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, bool includePrerelease, TopDocs topDocs)
        {
            jsonWriter.WritePropertyName("data");
            jsonWriter.WriteStartArray();

            if (topDocs.TotalHits > 0)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[0];

                var versions = includePrerelease ? searcher.Versions[scoreDoc.Doc].Versions : searcher.Versions[scoreDoc.Doc].StableVersions;

                foreach (var version in versions)
                {
                    jsonWriter.WriteValue(version);
                }
            }

            jsonWriter.WriteEndArray();
        }

        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // V3 find implementation
        //
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static string FindMakeResult(NuGetIndexSearcher searcher, string scheme, TopDocs topDocs)
        {
            Uri baseAddress = searcher.Manager.RegistrationBaseAddress[scheme];

            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteInfo(jsonWriter, null, searcher, topDocs);

                    if (topDocs.TotalHits > 0)
                    {
                        ScoreDoc scoreDoc = topDocs.ScoreDocs[0];
                        Document document = searcher.Doc(scoreDoc.Doc);
                        string id = document.Get("Id");

                        string relativeAddress = UriFormatter.MakeRegistrationRelativeAddress(id); 

                        WriteProperty(jsonWriter, "registration", new Uri(baseAddress, relativeAddress).AbsoluteUri);
                    }

                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // V2 search implementation - called from the NuGet Gallery
        //
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static string MakeCountResultV2(int totalHits)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteProperty(jsonWriter, "totalHits", totalHits);
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        static void WriteInfoV2(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs)
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

        static void WriteRegistrationV2(JsonTextWriter jsonWriter, Document document, int downloadCount)
        {
            jsonWriter.WritePropertyName("PackageRegistration");
            jsonWriter.WriteStartObject();
            
            WriteDocumentValue(jsonWriter, "Id", document, "Id");
            WriteProperty(jsonWriter, "DownloadCount", downloadCount);

            // TODO: missing owner in lucene
            jsonWriter.WritePropertyName("Owners");
            jsonWriter.WriteStartArray();
            foreach (string owner in document.GetValues("Owner"))
            {
                jsonWriter.WriteValue(owner);
            }
            jsonWriter.WriteEndArray();
            
            jsonWriter.WriteEndObject();
        }

        static void WriteDataV2(JsonTextWriter jsonWriter, NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take)
        {
            jsonWriter.WritePropertyName("Data");

            jsonWriter.WriteStartArray();

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
                Document document = searcher.Doc(scoreDoc.Doc);

                string version = document.Get("Version");

                Tuple<int, int> downloadCounts = NuGetIndexSearcher.GetDownloadCounts(searcher.Versions[scoreDoc.Doc], version);

                bool isLatest = searcher.LatestBitSet.Get(scoreDoc.Doc);
                bool isLatestStable = searcher.LatestStableBitSet.Get(scoreDoc.Doc);

                jsonWriter.WriteStartObject();
                WriteRegistrationV2(jsonWriter, document, downloadCounts.Item1);
                WriteDocumentValue(jsonWriter, "Version", document, "OriginalVersion");
                WriteProperty(jsonWriter, "NormalizedVersion", version);
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
                WriteDocumentValue(jsonWriter, "LastEdited", document, "OriginalEditedDate");
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
                WriteProperty(jsonWriter, "RequiresLicenseAcceptance", bool.Parse(document.Get("RequiresLicenseAcceptance") ?? "true"));
                WriteDocumentValue(jsonWriter, "LicenseNames", document, "LicenseNames");
                WriteDocumentValue(jsonWriter, "LicenseReportUrl", document, "LicenseReportUrl");
                WriteProperty(jsonWriter, "HideLicenseReport", bool.Parse(document.Get("HideLicenseReport") ?? "true"));   //TODO: data is missing from index
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
        }

        public static string MakeResultsV2(NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.WriteStartObject();
                    WriteInfoV2(jsonWriter, searcher, topDocs);
                    WriteDataV2(jsonWriter, searcher, topDocs, skip, take);
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                    stringWriter.Flush();

                    return stringWriter.ToString();
                }
            }
        }

        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // various generic helpers for building JSON results from Lucene Documents
        //
        // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static void WriteProperty<T>(JsonTextWriter jsonWriter, string propertyName, T value)
        {
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteValue(value);
        }

        static void WriteDocumentValue(JsonTextWriter jsonWriter, string propertyName, Document document, string fieldName)
        {
            string value = document.Get(fieldName);
            if (value != null)
            {
                WriteProperty(jsonWriter, propertyName, value);
            }
        }

        static void WriteDocumentValueAsArray(JsonTextWriter jsonWriter, string propertyName, Document document, string fieldName, bool singleElement = false)
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
                    foreach (var s in value.Split(' '))
                    {
                        jsonWriter.WriteValue(s);
                    }
                }

                jsonWriter.WriteEndArray();
            }
        }
    }
}
