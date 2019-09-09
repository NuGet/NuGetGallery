// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using NuGet.Indexing;
using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public partial class SearchTextBuilder : ISearchTextBuilder
    {
        public const string MatchAllDocumentsQuery = "*";
        private static readonly char[] PackageIdSeparators = new[] { '.', '-', '_' };

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

        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly NuGetQueryParser _parser;

        public SearchTextBuilder(IOptionsSnapshot<SearchServiceConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _parser = new NuGetQueryParser();
        }

        public ParsedQuery ParseV2Search(V2SearchRequest request)
        {
            var query = request.Query;

            // The old V2 search service would treat "id:" queries (~match) in the same way as it did "packageid:" (==match).
            // If "id:" is in the query, replace it.
            if (request.LuceneQuery && !string.IsNullOrEmpty(query) && query.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                query = "packageid:" + query.Substring(3);
            }

            return GetParsedQuery(query);
        }

        public ParsedQuery ParseV3Search(V3SearchRequest request)
        {
            return GetParsedQuery(request.Query);
        }

        public string Autocomplete(AutocompleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return MatchAllDocumentsQuery;
            }

            // Query package ids. If autocompleting package ids, allow prefix matches.
            var builder = new AzureSearchTextBuilder();

            builder.AppendScopedTerm(
                fieldName: IndexFields.PackageId,
                term: request.Query,
                prefixSearch: request.Type == AutocompleteRequestType.PackageIds);

            return builder.ToString();
        }

        private ParsedQuery GetParsedQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new ParsedQuery(new Dictionary<QueryField, HashSet<string>>());
            }

            var grouping = _parser.ParseQuery(query.Trim(), skipWhiteSpace: true);

            return new ParsedQuery(grouping);
        }

        public string Build(ParsedQuery parsed)
        {
            if (!parsed.Grouping.Any())
            {
                return MatchAllDocumentsQuery;
            }

            var scopedTerms = parsed.Grouping.Where(g => g.Key != QueryField.Any && g.Key != QueryField.Invalid).ToList();
            var unscopedTerms = parsed.Grouping.Where(g => g.Key == QueryField.Any)
                .Select(g => g.Value)
                .SingleOrDefault()?
                .ToList();

            // Don't bother generating Azure Search text if all terms are scoped to invalid fields.
            var hasUnscopedTerms = unscopedTerms != null && unscopedTerms.Count > 0;
            if (scopedTerms.Count == 0 && !hasUnscopedTerms)
            {
                return MatchAllDocumentsQuery;
            }

            // Add the terms that are scoped to specific fields.
            var builder = new AzureSearchTextBuilder();
            var requireScopedTerms = hasUnscopedTerms || scopedTerms.Count > 1;

            foreach (var scopedTerm in scopedTerms)
            {
                var fieldName = FieldNames[scopedTerm.Key];
                var values = ProcessFieldValues(scopedTerm.Key, scopedTerm.Value).ToList();

                if (values.Count == 0)
                {
                    // This happens if tags have only delimiters.
                    continue;
                }
                else if (values.Count > 1)
                {
                    builder.AppendScopedTerms(fieldName, values, required: requireScopedTerms);
                }
                else
                {
                    builder.AppendScopedTerm(fieldName, values.First(), required: requireScopedTerms);
                }
            }

            // Add the terms that can match any fields.
            if (hasUnscopedTerms)
            {
                builder.AppendTerms(unscopedTerms);

                // Generate a clause to favor results that match all unscoped terms.
                // We don't need to include scoped terms as these are already required.
                if (unscopedTerms.Count > 1)
                {
                    builder.AppendBoostIfMatchAllTerms(unscopedTerms, _options.Value.MatchAllTermsBoost);
                }
            }

            // Handle the exact match case. If the search query is a single unscoped term is also a valid package
            // ID, mega boost the document that has this package ID. Only consider the query to be a package ID has
            // symbols (a.k.a. separators) in it.
            if (scopedTerms.Count == 0
                && unscopedTerms.Count == 1
                && unscopedTerms[0].Length <= PackageIdValidator.MaxPackageIdLength
                && unscopedTerms[0].IndexOfAny(PackageIdSeparators) >= 0
                && PackageIdValidator.IsValidPackageId(unscopedTerms[0]))
            {
                builder.AppendExactMatchPackageIdBoost(unscopedTerms[0], _options.Value.ExactMatchBoost);
            }

            var result = builder.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return MatchAllDocumentsQuery;
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
