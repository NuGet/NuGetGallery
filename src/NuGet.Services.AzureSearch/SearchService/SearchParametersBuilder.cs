// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchParametersBuilder : ISearchParametersBuilder
    {
        public const int DefaultTake = 20;
        private const int MaximumTake = 1000;
        private const string Score = "search.score()";
        private const string Asc = " asc";
        private const string Desc = " desc";

        private static readonly List<string> LastCommitTimestampSelect = new List<string> { IndexFields.LastCommitTimestamp };
        private static readonly List<string> PackageIdsAutocompleteSelect = new List<string> { IndexFields.PackageId };
        private static readonly List<string> PackageVersionsAutocompleteSelect = new List<string> { IndexFields.Search.Versions };

        private static readonly List<string> LastCommitTimestampDescending = new List<string> { IndexFields.LastCommitTimestamp + Desc }; // Most recently added to the catalog first

        /// <summary>
        /// We use the created timestamp as a tie-breaker since it does not change for a given package.
        /// See: https://stackoverflow.com/a/34234258/52749
        /// </summary>
        private static readonly List<string> ScoreDesc = new List<string> { Score + Desc, IndexFields.Created + Desc }; // Highest score first ("most relevant"), then newest
        private static readonly List<string> LastEditedDesc = new List<string> { IndexFields.LastEdited + Desc, IndexFields.Created + Desc }; // Most recently edited first, then newest
        private static readonly List<string> PublishedDesc = new List<string> { IndexFields.Published + Desc, IndexFields.Created + Desc }; // Most recently published first, then newest
        private static readonly List<string> SortableTitleAsc = new List<string> { IndexFields.SortableTitle + Asc, IndexFields.Created + Asc }; // First title by lex order first, then newest
        private static readonly List<string> SortableTitleDesc = new List<string> { IndexFields.SortableTitle + Desc, IndexFields.Created + Desc }; // Last title by lex order first, then oldest
        private static readonly List<string> CreatedAsc = new List<string> { IndexFields.Created + Asc }; // Newest first
        private static readonly List<string> CreatedDesc = new List<string> { IndexFields.Created + Desc }; // Oldest first

        public SearchParameters LastCommitTimestamp()
        {
            return new SearchParameters
            {
                QueryType = QueryType.Full,
                Select = LastCommitTimestampSelect,
                OrderBy = LastCommitTimestampDescending,
                Skip = 0,
                Top = 1,
            };
        }

        public SearchParameters V2Search(V2SearchRequest request, bool isDefaultSearch)
        {
            var searchParameters = NewSearchParameters();

            if (request.CountOnly)
            {
                searchParameters.Skip = 0;
                searchParameters.Top = 0;
                searchParameters.OrderBy = null;
            }
            else
            {
                ApplyPaging(searchParameters, request);
                searchParameters.OrderBy = GetOrderBy(request.SortBy);
            }

            if (request.IgnoreFilter)
            {
                // Note that the prerelease flag has no effect when IgnoreFilter is true.

                if (!request.IncludeSemVer2)
                {
                    searchParameters.Filter = $"{IndexFields.SemVerLevel} ne {SemVerLevelKey.SemVer2}";
                }
            }
            else
            {
                ApplySearchIndexFilter(searchParameters, request, isDefaultSearch);
            }

            return searchParameters;
        }

        public SearchParameters V3Search(V3SearchRequest request, bool isDefaultSearch)
        {
            var searchParameters = NewSearchParameters();

            ApplyPaging(searchParameters, request);
            ApplySearchIndexFilter(searchParameters, request, isDefaultSearch);

            return searchParameters;
        }

        public SearchParameters Autocomplete(AutocompleteRequest request, bool isDefaultSearch)
        {
            var searchParameters = NewSearchParameters();

            ApplySearchIndexFilter(searchParameters, request, isDefaultSearch);

            switch (request.Type)
            {
                case AutocompleteRequestType.PackageIds:
                    searchParameters.Select = PackageIdsAutocompleteSelect;
                    ApplyPaging(searchParameters, request);
                    break;
                
                // Package version autocomplete should only match a single document
                // regardless of the request's parameters.
                case AutocompleteRequestType.PackageVersions:
                    searchParameters.Select = PackageVersionsAutocompleteSelect;
                    searchParameters.Skip = 0;
                    searchParameters.Top = 1;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown autocomplete request type '{request.Type}'");
            }

            return searchParameters;
        }

        private static SearchParameters NewSearchParameters()
        {
            return new SearchParameters
            {
                IncludeTotalResultCount = true,
                QueryType = QueryType.Full,
                OrderBy = ScoreDesc,
            };
        }

        private static void ApplyPaging(SearchParameters searchParameters, SearchRequest request)
        {
            searchParameters.Skip = request.Skip < 0 ? 0 : request.Skip;
            searchParameters.Top = request.Take < 0 || request.Take > MaximumTake ? DefaultTake : request.Take;
        }

        private void ApplySearchIndexFilter(SearchParameters searchParameters, SearchRequest request, bool excludePackagesHiddenByDefault)
        {
            var searchFilters = GetSearchFilters(request);

            searchParameters.Filter = GetFilterString(searchFilters, excludePackagesHiddenByDefault);
        }

        public SearchFilters GetSearchFilters(SearchRequest request)
        {
            var searchFilters = SearchFilters.Default;

            if (request.IncludePrerelease)
            {
                searchFilters |= SearchFilters.IncludePrerelease;
            }

            if (request.IncludeSemVer2)
            {
                searchFilters |= SearchFilters.IncludeSemVer2;
            }

            return searchFilters;
        }

        private static string GetFilterString(SearchFilters searchFilters, bool excludePackagesHiddenByDefault)
        {
            var filterString = $"{IndexFields.Search.SearchFilters} eq '{DocumentUtilities.GetSearchFilterString(searchFilters)}'";

            if (excludePackagesHiddenByDefault)
            {
                filterString += $" and ({IndexFields.Search.IsExcludedByDefault} eq false or {IndexFields.Search.IsExcludedByDefault} eq null)";
            }

            return filterString;
        }

        private static IList<string> GetOrderBy(V2SortBy sortBy)
        {
            IList<string> orderBy;
            switch (sortBy)
            {
                case V2SortBy.Popularity:
                    orderBy = ScoreDesc;
                    break;
                case V2SortBy.LastEditedDesc:
                    orderBy = LastEditedDesc;
                    break;
                case V2SortBy.PublishedDesc:
                    orderBy = PublishedDesc;
                    break;
                case V2SortBy.SortableTitleAsc:
                    orderBy = SortableTitleAsc;
                    break;
                case V2SortBy.SortableTitleDesc:
                    orderBy = SortableTitleDesc;
                    break;
                case V2SortBy.CreatedAsc:
                    orderBy = CreatedAsc;
                    break;
                case V2SortBy.CreatedDesc:
                    orderBy = CreatedDesc;
                    break;
                default:
                    throw new ArgumentException($"The provided {nameof(V2SortBy)} is not supported.", nameof(sortBy));
            }

            return orderBy;
        }
    }
}
