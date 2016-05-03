// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json;
using NuGet.Versioning;

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

        public static LuceneCommitMetadata CreateCommitMetadata(DateTime commitTimeStamp, string description, int count, string trace)
        {
            return new LuceneCommitMetadata(commitTimeStamp, description, count, trace);
        }
        
        public static Document CreateDocument(IDictionary<string, string> package)
        {
            var errors = new List<string>();
            var document = new Document();

            // add fields used by search queries
            AddId(document, package, errors);
            AddVersion(document, package, errors);
            AddTitle(document, package);
            AddField(document, "Description", package, "description", Field.Index.ANALYZED);
            AddField(document, "Summary", package, "summary", Field.Index.ANALYZED);
            AddField(document, "Tags", package, "tags", Field.Index.ANALYZED, 2.0f);
            AddField(document, "Authors", package, "authors", Field.Index.ANALYZED);

            // add fields used by filtering and sorting
            AddListed(document, package, errors);
            AddDates(document, package, errors);
            AddSortableTitle(document, package);

            // add fields used when materializing the result
            AddField(document, "IconUrl", package, "iconUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "ProjectUrl", package, "projectUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "MinClientVersion", package, "minClientVersion", Field.Index.NOT_ANALYZED);
            AddField(document, "ReleaseNotes", package, "releaseNotes", Field.Index.NOT_ANALYZED);
            AddField(document, "Copyright", package, "copyright", Field.Index.NOT_ANALYZED);
            AddField(document, "Language", package, "language", Field.Index.NOT_ANALYZED);
            AddField(document, "LicenseUrl", package, "licenseUrl", Field.Index.NOT_ANALYZED);
            AddField(document, "PackageHash", package, "packageHash", Field.Index.NOT_ANALYZED);
            AddField(document, "PackageHashAlgorithm", package, "packageHashAlgorithm", Field.Index.NOT_ANALYZED);
            AddPackageSize(document, package, errors);
            AddRequiresLicenseAcceptance(document, package, errors);
            AddDependencies(document, package);
            AddSupportedFrameworks(document, package);

            DetermineLanguageBoost(document, package);
            CheckErrors(errors);

            return document;
        }

        static void AddId(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue("id", out value))
            {
                float boost = 2.0f;
                if (!package.ContainsKey("tags"))
                {
                    boost += 0.5f;
                }

                AddField(document, "Id", value, Field.Index.ANALYZED, boost);
                AddField(document, "IdAutocomplete", value, Field.Index.ANALYZED, boost);
                AddField(document, "TokenizedId", value, Field.Index.ANALYZED, boost);
                AddField(document, "ShingledId", value, Field.Index.ANALYZED, boost);
            }
            else
            {
                errors.Add("Required property 'id' not found.");
            }
        }

        static void AddVersion(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string originalVersion;
            if (package.TryGetValue("originalVersion", out originalVersion))
            {
                AddField(document, "OriginalVersion", originalVersion, Field.Index.NOT_ANALYZED);
            }

            string version;
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
                        errors.Add("Unable to parse 'originalVersion' as NuGetVersion.");
                    }
                }
            }

            if (version != null)
            {
                AddField(document, "Version", version, Field.Index.ANALYZED);
            }
            else
            {
                errors.Add("Required property 'version' or 'originalVersion' not found.");
            }
        }

        static void AddTitle(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue("title", out value) || package.TryGetValue("id", out value))
            {
                AddField(document, "Title", value ?? string.Empty, Field.Index.ANALYZED);
            }
        }

        static void AddListed(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue("listed", out value))
            {
                bool listed;
                if (bool.TryParse(value, out listed))
                {
                    AddField(document, "Listed", value, Field.Index.ANALYZED);
                }
                else
                {
                    errors.Add("Unable to parse 'listed' as Boolean.");
                }
            }
            else
            {
                errors.Add("Required property 'listed' not found.");
            }
        }

        static void AddSortableTitle(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue("title", out value) || package.TryGetValue("id", out value))
            {
                AddField(document, "SortableTitle", (value ?? string.Empty).ToLower(), Field.Index.ANALYZED);
            }
        }

        static void AddDates(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string created;
            if (package.TryGetValue("created", out created))
            {
                AddField(document, "OriginalCreated", created, Field.Index.NOT_ANALYZED);
            }

            string published;
            if (package.TryGetValue("published", out published))
            {
                AddField(document, "OriginalPublished", published, Field.Index.NOT_ANALYZED);

                DateTimeOffset publishedDateTime;
                if (DateTimeOffset.TryParse(published, out publishedDateTime))
                {
                    AddDateField(document, "PublishedDate", publishedDateTime);
                }
                else
                {
                    errors.Add("Unable to parse 'published' as DateTime.");
                }

                string lastEdited;
                if (package.TryGetValue("lastEdited", out lastEdited) && lastEdited != "01/01/0001 00:00:00")
                {
                    AddField(document, "OriginalLastEdited", lastEdited, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    lastEdited = publishedDateTime.ToString("O");
                }

                DateTimeOffset lastEditedDateTime;
                if (DateTimeOffset.TryParse(lastEdited, out lastEditedDateTime))
                {
                    AddDateField(document, "LastEditedDate", lastEditedDateTime);
                }
                else
                {
                    errors.Add("Unable to parse 'lastEdited' as DateTime.");
                }
            }
            else
            {
                errors.Add("Required property 'published' not found.");
            }
        }

        static void AddPackageSize(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue("packageSize", out value))
            {
                int packageSize;
                if (int.TryParse(value, out packageSize))
                {
                    AddField(document, "PackageSize", value, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    errors.Add("Unable to parse 'packageSize' as Int32.");
                }
            }
        }

        static void AddRequiresLicenseAcceptance(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue("requireLicenseAcceptance", out value))
            {
                bool requiresLicenseAcceptance;
                if (bool.TryParse(value, out requiresLicenseAcceptance))
                {
                    AddField(document, "RequiresLicenseAcceptance", value, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    errors.Add("Unable to parse 'requiresLicenseAcceptance' as Boolean.");
                }
            }
        }

        static void AddDependencies(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue("flattenedDependencies", out value))
            {
                AddField(document, "FlattenedDependencies", value, Field.Index.NOT_ANALYZED);

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

                            AddField(document, "Dependencies", dependencies, Field.Index.NOT_ANALYZED);
                        }
                    }
                }
            }
        }

        static void AddSupportedFrameworks(Document document, IDictionary<string, string> package)
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

        static void DetermineLanguageBoost(Document document, IDictionary<string, string> package)
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

        static void CheckErrors(List<string> errors)
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

        static void AddField(Document document, string destination, IDictionary<string, string> package, string source, Field.Index index, float boost = 1.0f)
        {
            string value;
            if (package.TryGetValue(source, out value))
            {
                AddField(document, destination, value, index, boost);
            }
            else if (index == Field.Index.ANALYZED)
            {
                /*
                 * Analyzed fields are those that are used in queries. There is a problem in the ParallelReader that
                 * cases a KeyNotFoundException to be thrown when querying for a field that does not exist in a
                 * document. Therefore, we add an empty value for fields that would otherwise not be present in the
                 * document.
                 */
                AddField(document, destination, string.Empty, index, boost);
            }
        }

        static void AddDateField(Document document, string destination, DateTimeOffset date)
        {
            document.Add(new NumericField(destination, Field.Store.YES, true).SetIntValue(int.Parse(date.ToString("yyyyMMdd"))));
        }

        static void AddField(Document document, string destination, string value, Field.Index index, float boost = 1.0f)
        {
            var termVector = index == Field.Index.ANALYZED ? Field.TermVector.WITH_POSITIONS_OFFSETS : Field.TermVector.NO;
            document.Add(new Field(destination, value, Field.Store.YES, index, termVector) { Boost = boost });
        }
    }
}
