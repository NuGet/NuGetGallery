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
using LuceneConstants = NuGet.Indexing.MetadataConstants.LuceneMetadata;

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
            AddField(document, LuceneConstants.DescriptionPropertyName, package, MetadataConstants.DescriptionPropertyName, Field.Index.ANALYZED);
            AddField(document, LuceneConstants.SummaryPropertyName, package, MetadataConstants.SummaryPropertyName, Field.Index.ANALYZED);
            AddField(document, LuceneConstants.TagsPropertyName, package, MetadataConstants.TagsPropertyName, Field.Index.ANALYZED, 2.0f);
            AddField(document, LuceneConstants.AuthorsPropertyName, package, MetadataConstants.AuthorsPropertyName, Field.Index.ANALYZED);

            // add fields used by filtering and sorting
            AddField(document, LuceneConstants.SemVerLevelPropertyName, package, MetadataConstants.SemVerLevelKeyPropertyName, Field.Index.ANALYZED);
            AddListed(document, package, errors);
            AddDates(document, package, errors);
            AddSortableTitle(document, package);

            // add fields used when materializing the result
            AddField(document, LuceneConstants.IconUrlPropertyName, package, MetadataConstants.IconUrlPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.ProjectUrlPropertyName, package, MetadataConstants.ProjectUrlPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.MinClientVersionPropertyName, package, MetadataConstants.MinClientVersionPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.ReleaseNotesPropertyName, package, MetadataConstants.ReleaseNotesPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.CopyrightPropertyName, package, MetadataConstants.CopyrightPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.LanguagePropertyName, package, MetadataConstants.LanguagePropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.LicenseUrlPropertyName, package, MetadataConstants.LicenseUrlPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.PackageHashPropertyName, package, MetadataConstants.PackageHashPropertyName, Field.Index.NOT_ANALYZED);
            AddField(document, LuceneConstants.PackageHashAlgorithmPropertyName, package, MetadataConstants.PackageHashAlgorithmPropertyName, Field.Index.NOT_ANALYZED);
            AddPackageSize(document, package, errors);
            AddRequiresLicenseAcceptance(document, package, errors);
            AddDependencies(document, package);
            AddSupportedFrameworks(document, package);

            DetermineLanguageBoost(document, package);
            CheckErrors(errors);

            return document;
        }

        private static void AddId(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.IdPropertyName, out value))
            {
                float boost = 2.0f;
                if (!package.ContainsKey(MetadataConstants.TagsPropertyName))
                {
                    boost += 0.5f;
                }

                AddField(document, LuceneConstants.IdPropertyName, value, Field.Index.ANALYZED, boost);
                AddField(document, LuceneConstants.IdAutocompletePropertyName, value, Field.Index.ANALYZED, boost);
                AddField(document, LuceneConstants.TokenizedIdPropertyName, value, Field.Index.ANALYZED, boost);
                AddField(document, LuceneConstants.ShingledIdPropertyName, value, Field.Index.ANALYZED, boost);
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.IdPropertyName}' not found.");
            }
        }

        private static void AddVersion(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string verbatimVersion;
            if (package.TryGetValue(MetadataConstants.VerbatimVersionPropertyName, out verbatimVersion))
            {
                AddField(document, LuceneConstants.VerbatimVersionPropertyName, verbatimVersion, Field.Index.NOT_ANALYZED);

                NuGetVersion parsedVerbatimVersion;
                if (NuGetVersion.TryParse(verbatimVersion, out parsedVerbatimVersion))
                {
                    AddField(document, LuceneConstants.NormalizedVersionPropertyName, parsedVerbatimVersion.ToNormalizedString(), Field.Index.ANALYZED);
                    AddField(document, LuceneConstants.FullVersionPropertyName, parsedVerbatimVersion.ToFullString(), Field.Index.NOT_ANALYZED);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.VerbatimVersionPropertyName}' as NuGetVersion.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.VerbatimVersionPropertyName}' not found.");
            }
        }

        private static void AddTitle(Document document, IDictionary<string, string> package)
        {
            string value;

            package.TryGetValue(MetadataConstants.TitlePropertyName, out value);

            if (string.IsNullOrEmpty(value))
            {
                package.TryGetValue(MetadataConstants.IdPropertyName, out value);
            }

            AddField(document, LuceneConstants.TitlePropertyName, value ?? string.Empty, Field.Index.ANALYZED);
        }

        private static void AddListed(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.ListedPropertyName, out value))
            {
                bool listed;
                if (bool.TryParse(value, out listed))
                {
                    AddField(document, LuceneConstants.ListedPropertyName, value, Field.Index.ANALYZED);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.ListedPropertyName}' as Boolean.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.ListedPropertyName}' not found.");
            }
        }

        private static void AddSortableTitle(Document document, IDictionary<string, string> package)
        {
            string value;

            package.TryGetValue(MetadataConstants.TitlePropertyName, out value);

            if (string.IsNullOrEmpty(value))
            {
                package.TryGetValue(MetadataConstants.IdPropertyName, out value);
            }

            AddField(document, LuceneConstants.SortableTitlePropertyName, (value ?? string.Empty).Trim().ToLower(), Field.Index.NOT_ANALYZED);
        }

        private static void AddDates(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string created;
            if (package.TryGetValue(MetadataConstants.CreatedPropertyName, out created))
            {
                AddField(document, LuceneConstants.OriginalCreatedPropertyName, created, Field.Index.NOT_ANALYZED);
            }

            string published;
            if (package.TryGetValue(MetadataConstants.PublishedPropertyName, out published))
            {
                AddField(document, LuceneConstants.OriginalPublishedPropertyName, published, Field.Index.NOT_ANALYZED);

                DateTimeOffset publishedDateTime;
                if (DateTimeOffset.TryParse(published, out publishedDateTime))
                {
                    AddDateField(document, LuceneConstants.PublishedDatePropertyName, publishedDateTime);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.PublishedPropertyName}' as DateTime.");
                }

                string lastEdited;
                if (package.TryGetValue(MetadataConstants.LastEditedPropertyName, out lastEdited) && lastEdited != MetadataConstants.DateTimeZeroStringValue)
                {
                    AddField(document, LuceneConstants.OriginalLastEditedPropertyName, lastEdited, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    lastEdited = publishedDateTime.ToString("O");
                }

                DateTimeOffset lastEditedDateTime;
                if (DateTimeOffset.TryParse(lastEdited, out lastEditedDateTime))
                {
                    AddDateField(document, LuceneConstants.LastEditedDatePropertyName, lastEditedDateTime);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.LastEditedPropertyName}' as DateTime.");
                }
            }
            else
            {
                errors.Add($"Required property '{MetadataConstants.PublishedPropertyName}' not found.");
            }
        }

        private static void AddPackageSize(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.PackageSizePropertyName, out value))
            {
                int packageSize;
                if (int.TryParse(value, out packageSize))
                {
                    AddField(document, LuceneConstants.PackageSizePropertyName, value, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.PackageSizePropertyName}' as Int32.");
                }
            }
        }

        private static void AddRequiresLicenseAcceptance(Document document, IDictionary<string, string> package, List<string> errors)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.RequiresLicenseAcceptancePropertyName, out value))
            {
                bool requiresLicenseAcceptance;
                if (bool.TryParse(value, out requiresLicenseAcceptance))
                {
                    AddField(document, LuceneConstants.RequiresLicenseAcceptancePropertyName, value, Field.Index.NOT_ANALYZED);
                }
                else
                {
                    errors.Add($"Unable to parse '{MetadataConstants.RequiresLicenseAcceptancePropertyName}' as Boolean.");
                }
            }
        }

        private static void AddDependencies(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.FlattenedDependenciesPropertyName, out value))
            {
                AddField(document, LuceneConstants.FlattenedDependenciesPropertyName, value, Field.Index.NOT_ANALYZED);

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

                            AddField(document, LuceneConstants.DependenciesPropertyName, dependencies, Field.Index.NOT_ANALYZED);
                        }
                    }
                }
            }
        }

        private static void AddSupportedFrameworks(Document document, IDictionary<string, string> package)
        {
            string value;
            if (package.TryGetValue(MetadataConstants.SupportedFrameworksPropertyName, out value))
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

                        document.Add(new Field(LuceneConstants.SupportedFrameworksPropertyName, supportedFrameworks, Field.Store.YES, Field.Index.NOT_ANALYZED));
                    }
                }
            }
        }

        private static void DetermineLanguageBoost(Document document, IDictionary<string, string> package)
        {
            string id;
            string language;
            if (package.TryGetValue(MetadataConstants.IdPropertyName, out id) && package.TryGetValue(MetadataConstants.LanguagePropertyName, out language))
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

        private static void AddField(Document document, string destination, IDictionary<string, string> package, string source, Field.Index index, float boost = 1.0f)
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

        private static void AddDateField(Document document, string destination, DateTimeOffset date)
        {
            document.Add(new NumericField(destination, Field.Store.YES, true).SetIntValue(int.Parse(date.ToString("yyyyMMdd"))));
        }

        private static void AddField(Document document, string destination, string value, Field.Index index, float boost = 1.0f)
        {
            var termVector = index == Field.Index.ANALYZED
                ? Field.TermVector.WITH_POSITIONS_OFFSETS
                : Field.TermVector.NO;

            document.Add(
                new Field(destination, value, Field.Store.YES, index, termVector)
                {
                    Boost = boost
                });
        }
    }
}
