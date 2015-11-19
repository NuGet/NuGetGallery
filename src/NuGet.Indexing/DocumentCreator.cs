// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NuGet.Indexing
{
    public static class DocumentCreator
    {
        const int MergeFactor = 10;        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;     //  Except never merge segments that have more docs than this 

        public static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory, bool create)
        {
            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.MergeFactor = MergeFactor;
            indexWriter.MaxMergeDocs = MaxMergeDocs;

            indexWriter.SetSimilarity(new CustomSimilarity());
            return indexWriter;
        }

        public static IDictionary<string, string> CreateCommitMetadata(string description, string trace)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();
            commitMetadata.Add("commitTimeStamp", DateTime.UtcNow.ToString("O"));
            commitMetadata.Add("description", description);
            commitMetadata.Add("trace", trace);
            return commitMetadata;
        }

        public static Document CreateDocument(IDictionary<string, string> package)
        {
            var errors = new List<string>();
            var document = new Document();

            AddId(document, package, errors);
            AddVersion(document, package, errors);
            AddFieldWithDefault(document, "Title", package, "title", package["id"], Field.Index.ANALYZED, 2.0f);
            AddField(document, "Description", package, "description", Field.Index.ANALYZED);
            AddField(document, "Summary", package, "summary", Field.Index.ANALYZED);
            AddField(document, "Tags", package, "tags", Field.Index.ANALYZED, 2.0f);
            AddField(document, "Authors", package, "flattenedAuthors", Field.Index.ANALYZED);

            AddField(document, "IconUrl", package, "iconUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "ProjectUrl", package, "projectUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "MinClientVersion", package, "minClientVersion", Field.Index.NOT_ANALYZED);
            AddField(document, "ReleaseNotes", package, "releaseNotes", Field.Index.NOT_ANALYZED);
            AddField(document, "Copyright", package, "copyright", Field.Index.NOT_ANALYZED);
            AddField(document, "Language", package, "language", Field.Index.NOT_ANALYZED);
            AddField(document, "LicenseUrl", package, "licenseUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "RequiresLicenseAcceptance", package, "requiresLicenseAcceptance", Field.Index.NOT_ANALYZED);
            AddField(document, "PackageHash", package, "packageHash", Field.Index.NOT_ANALYZED);
            AddField(document, "PackageHashAlgorithm", package, "packageHashAlgorithm", Field.Index.NOT_ANALYZED);
            AddField(document, "PackageSize", package, "packageSize", Field.Index.NOT_ANALYZED);
            AddDependencies(document, package);
            AddField(document, "LicenseNames", package, "licenseNames", Field.Index.NOT_ANALYZED);
            AddField(document, "LicenseReportUrl", package, "licenseReportUrl", Field.Index.NOT_ANALYZED);
            AddDates(document, package, errors);
            AddSupportedFrameworks(document, package);

            AddRequiredField(document, "Listed", package, errors, "listed", Field.Index.NOT_ANALYZED);

            DetermineLanguageBoost(document, package);

            CheckErrors(errors);

            return document;
        }

        private static void CheckErrors(List<string> errors)
        {
            if (errors.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (string error in errors)
                {
                    sb.AppendLine(error);
                }
                throw new Exception(sb.ToString());
            }
        }

        private static void AddId(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue("id", out value))
            {
                float boost = 2.0f;
                if (!package.ContainsKey("tags"))
                {
                    boost += 0.5f;
                }
                document.Add(new Field("Id", value, Field.Store.YES, Field.Index.ANALYZED) { Boost = boost });
                document.Add(new Field("IdAutocomplete", value, Field.Store.NO, Field.Index.ANALYZED) { Boost = boost });
                document.Add(new Field("TokenizedId", value, Field.Store.NO, Field.Index.ANALYZED) { Boost = boost });
                document.Add(new Field("ShingledId", value, Field.Store.NO, Field.Index.ANALYZED) { Boost = boost });
            }
            else
            {
                errors.Add("Required property 'id' not found.");
            }
        }

        private static void AddVersion(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string originalVersion = null;
            if (package.TryGetValue("originalVersion", out originalVersion))
            {
                document.Add(new Field("OriginalVersion", originalVersion, Field.Store.YES, Field.Index.NOT_ANALYZED));
            }

            string version = null;
            if (!package.TryGetValue("version", out version))
            {
                if (originalVersion != null)
                {
                    NuGetVersion nuGetVersion;
                    if (NuGetVersion.TryParse(originalVersion, out nuGetVersion))
                    {
                        version = nuGetVersion.ToNormalizedString();
                    }
                    else
                    {
                        errors.Add("Unable to parse 'originalVersion' property.");
                    }
                }
            }

            if (version != null)
            {
                document.Add(new Field("Version", version, Field.Store.YES, Field.Index.ANALYZED));
            }
            else
            {
                errors.Add("Required property 'version' not found.");
            }
        }

        private static void AddDependencies(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue("flattenedDependencies", out value))
            {
                document.Add(new Field("FlattenedDependencies", value, Field.Store.YES, Field.Index.NOT_ANALYZED));

                if (!string.IsNullOrWhiteSpace(value))
                {
                    using (var textWriter = new StringWriter())
                    {
                        using (var jsonWriter = new JsonTextWriter(textWriter))
                        {
                            jsonWriter.WriteStartArray();

                            foreach (var dependency in value.Split('|'))
                            {
                                string[] fields = dependency.Split(':');
                                if (fields.Length > 0)
                                {
                                    jsonWriter.WriteStartObject();
                                    jsonWriter.WritePropertyName("Id");
                                    jsonWriter.WriteValue(fields[0]);
                                    if (fields.Length > 1)
                                    {
                                        jsonWriter.WritePropertyName("VersionSpec");
                                        jsonWriter.WriteValue(fields[1]);
                                    }
                                    if (fields.Length > 2)
                                    {
                                        jsonWriter.WritePropertyName("TargetFramework");
                                        jsonWriter.WriteValue(fields[2]);
                                    }
                                    jsonWriter.WriteEndObject();
                                }
                            }
                            jsonWriter.WriteEndArray();
                            jsonWriter.Flush();
                            textWriter.Flush();
                            string dependencies = textWriter.ToString();

                            document.Add(new Field("Dependencies", dependencies, Field.Store.YES, Field.Index.NOT_ANALYZED));
                        }
                    }
                }
            }
        }

        private static void AddDates(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string created;
            if (package.TryGetValue("created", out created))
            {
                document.Add(new Field("OriginalCreated", created, Field.Store.YES, Field.Index.NOT_ANALYZED));
            }

            string published;
            if (package.TryGetValue("published", out published))
            {
                document.Add(new Field("OriginalPublished", published, Field.Store.YES, Field.Index.NOT_ANALYZED));

                DateTime publishedDateTime;
                if (DateTime.TryParse(published, out publishedDateTime))
                {
                    document.Add(new NumericField("Published", Field.Store.YES, true).SetIntValue(int.Parse(publishedDateTime.ToString("yyyyMMdd"))));
                }
                else
                {
                    errors.Add("Unable to parse 'published' as DateTime");
                }

                string lastEdited;
                if (package.TryGetValue("lastEdited", out lastEdited))
                {
                    document.Add(new Field("OriginalLastEdited", lastEdited, Field.Store.YES, Field.Index.NOT_ANALYZED));
                }
                else
                {
                    lastEdited = published;
                }

                DateTime lastEditedDateTime;
                if (DateTime.TryParse(lastEdited, out lastEditedDateTime))
                {
                    document.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEditedDateTime.ToString("yyyyMMdd"))));
                }
                else
                {
                    errors.Add("Unable to parse 'lastEdited' as DateTime");
                }
            }
            else
            {
                errors.Add("Required property 'published' not found.");
            }
        }

        private static void AddSupportedFrameworks(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue("supportedFrameworks", out value))
            {
                using (var textWriter = new StringWriter())
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.WriteStartArray();
                        foreach (var s in value.Split('|'))
                        {
                            jsonWriter.WriteValue(s);
                        }
                        jsonWriter.WriteEndArray();
                        jsonWriter.Flush();
                        textWriter.Flush();
                        string supportedFrameworks = textWriter.ToString();

                        document.Add(new Field("SupportedFrameworks", supportedFrameworks, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    }
                }
            }
        }

        private static void AddRequiredField(
            Document document,
            string destName,
            IDictionary<string, string> package,
            List<string> errors,
            string sourceName,
            Field.Index fieldIndex,
            float boost = 1.0f)
        {
            if (!AddField(document, destName, package, sourceName, fieldIndex, boost))
            {
                errors.Add($"Required property '{sourceName}' not found.");
            }
        }

        private static void AddFieldWithDefault(
            Document document,
            string destName,
            IDictionary<string, string> package,
            string sourceName,
            string defaultValue,
            Field.Index fieldIndex,
            float boost = 1.0f)
        {
            if (!AddField(document, destName, package, sourceName, fieldIndex, boost))
            {
                document.Add(new Field(destName, defaultValue, Field.Store.YES, fieldIndex) { Boost = boost });
            }
        }

        private static bool AddField(
            Document document,
            string destName,
            IDictionary<string, string> package,
            string sourceName,
            Field.Index fieldIndex,
            float boost = 1.0f)
        {
            string value;
            if (package.TryGetValue(sourceName, out value))
            {
                document.Add(new Field(destName, value, Field.Store.YES, fieldIndex) { Boost = boost });
                return true;
            }

            return false;
        }

        private static void DetermineLanguageBoost(Document document, IDictionary<string, string> package)
        {
            string id;
            string language;
            if (package.TryGetValue("id", out id) && package.TryGetValue("language", out language))
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    string languageSuffix = "." + language.Trim();
                    if (id.EndsWith(languageSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        document.Boost = 0.1f;
                    }
                }
                document.Boost = 1.0f;
            }
        }
    }
}
