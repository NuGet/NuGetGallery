// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Indexing;
using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public partial class SearchTextBuilder : ISearchTextBuilder
    {
        private static readonly ParsedQuery MatchAllDocumentsQuery = new ParsedQuery("*", packageId: null);

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

        public ParsedQuery V2Search(V2SearchRequest request)
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

        public ParsedQuery V3Search(V3SearchRequest request)
        {
            return GetLuceneQuery(request.Query);
        }

        public string Autocomplete(AutocompleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return MatchAllDocumentsQuery.Text;
            }

            // Generate a query on package ids. This will be a prefix search
            // if we are autocompleting package ids.
            var builder = new AzureSearchQueryBuilder();

            builder.AppendScopedTerms(
                IndexFields.PackageId,
                new[] { request.Query },
                prefixSearch: request.Type == AutocompleteRequestType.PackageIds);

            var result = builder.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return MatchAllDocumentsQuery.Text;
            }

            return result;
        }

        private ParsedQuery GetLuceneQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return MatchAllDocumentsQuery;
            }

            // Parse the NuGet query.
            var grouping = _parser.ParseQuery(query.Trim(), skipWhiteSpace: true);
            if (!grouping.Any())
            {
                return MatchAllDocumentsQuery;
            }

            string packageId = null;
            if (grouping.Count == 1
                && grouping.TryGetValue(QueryField.PackageId, out var terms)
                && terms.Count == 1)
            {
                packageId = terms.First();

                if (packageId.Length > PackageIdValidator.MaxPackageIdLength
                    || !PackageIdValidator.IsValidPackageId(packageId))
                {
                    packageId = null;
                }
            }

            // Generate a Lucene query for Azure Search.
            var builder = new AzureSearchQueryBuilder();
            var scopedTerms = grouping.Where(g => g.Key != QueryField.Any && g.Key != QueryField.Invalid).ToList();
            var unscopedTerms = grouping.Where(g => g.Key == QueryField.Any)
                .Select(g => g.Value)
                .SingleOrDefault();

            var requireScopedTerms = unscopedTerms != null
                || scopedTerms.Select(t => t.Key).Distinct().Count() > 1;

            foreach (var scopedTerm in scopedTerms)
            {
                var fieldName = FieldNames[scopedTerm.Key];
                var values = ProcessFieldValues(scopedTerm.Key, scopedTerm.Value).ToList();

                builder.AppendScopedTerms(fieldName, values, required: requireScopedTerms);
            }

            if (unscopedTerms != null)
            {
                builder.AppendTerms(unscopedTerms.ToList());
            }

            var result = builder.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return MatchAllDocumentsQuery;
            }

            return new ParsedQuery(result, packageId);
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
