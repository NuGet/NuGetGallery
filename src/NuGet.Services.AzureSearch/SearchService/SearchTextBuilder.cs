// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Indexing;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchTextBuilder : ISearchTextBuilder
    {
        private const string MatchAllDocumentsQuery = "*";

        private static readonly IReadOnlyDictionary<QueryField, string> FieldNames = new Dictionary<QueryField, string>
        {
            { QueryField.Author, IndexFields.Authors },
            { QueryField.Description, IndexFields.Description },
            { QueryField.Id, IndexFields.TokenizedPackageId },
            { QueryField.Owner, IndexFields.Search.Owners },
            { QueryField.PackageId, IndexFields.PackageId },
            { QueryField.Summary, IndexFields.Summary },
            { QueryField.Tag, IndexFields.Tags },
            { QueryField.Title, IndexFields.Title },
            { QueryField.Version, IndexFields.NormalizedVersion },
        };

        private readonly NuGetQueryParser _parser;

        public SearchTextBuilder()
        {
            _parser = new NuGetQueryParser();
        }

        public string V2Search(V2SearchRequest request)
        {
            var query = request.Query;

            // The old V2 search service would treat "id:" queries (~match) in the same way as it did "packageid:" (==match).
            // If "id:" is in the query, replace it.
            if (request.LuceneQuery && !string.IsNullOrEmpty(query) && query.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                query = "packageid:" + query.Substring(3);
            }

            return GetLuceneQuery(query);
        }

        public string V3Search(V3SearchRequest request)
        {
            return GetLuceneQuery(request.Query);
        }

        private string GetLuceneQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return MatchAllDocumentsQuery;
            }

            var grouping = _parser.ParseQuery(query.Trim());
            if (!grouping.Any())
            {
                return MatchAllDocumentsQuery;
            }

            var result = ToAzureSearchQuery(grouping).ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return MatchAllDocumentsQuery;
            }

            return result;
        }

        private AzureSearchQueryBuilder ToAzureSearchQuery(Dictionary<QueryField, HashSet<string>> grouping)
        {
            var result = new AzureSearchQueryBuilder();

            foreach (var field in grouping)
            {
                // Add values that aren't scoped to a field.
                if (field.Key == QueryField.Any)
                {
                    result.AddNonFieldScopedValues(field.Value);
                }
                else if (field.Key != QueryField.Invalid)
                {
                    // Add values that are scoped to a valid field.
                    var fieldName = FieldNames[field.Key];
                    var values = ProcessFieldValues(field.Key, field.Value);

                    result.AddFieldScopedValues(fieldName, values);
                }
             }

            return result;
        }

        private static IEnumerable<string> ProcessFieldValues(QueryField field, IEnumerable<string> values)
        {
            switch (field)
            {
                // Expand tags by their delimiters
                case QueryField.Tag:
                    return values.SelectMany(Utils.SplitTags).Distinct();

                // The "version" query field should be normalized if possible.
                case QueryField.Version:
                    return values.Select(value =>
                    {
                        if (!NuGetVersion.TryParse(value, out var version))
                        {
                            return value;
                        }

                        return version.ToNormalizedString();
                    });

                default:
                    return values;
            }
        }
    }
}
