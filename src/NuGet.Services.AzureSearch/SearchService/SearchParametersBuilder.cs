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

        private static readonly List<string> LastCommitTimestampSelect = new List<string> { IndexFields.LastCommitTimestamp };
        private static readonly List<string> PackageIdsAutocompleteSelect = new List<string> { IndexFields.PackageId };
        private static readonly List<string> PackageVersionsAutocompleteSelect = new List<string> { IndexFields.Search.Versions };

        private static readonly string Ascending = " asc";
        private static readonly string Descending = " desc";
        private static readonly List<string> LastCommitTimestampDescending = new List<string> { IndexFields.LastCommitTimestamp + Descending };
        private static readonly List<string> LastEditedDescending = new List<string> { IndexFields.LastEdited + Descending };
        private static readonly List<string> PublishedDescending = new List<string> { IndexFields.Published + Descending };
        private static readonly List<string> SortableTitleAscending = new List<string> { IndexFields.SortableTitle + Ascending };
        private static readonly List<string> SortableTitleDescending = new List<string> { IndexFields.SortableTitle + Descending };

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

            filterString += excludePackagesHiddenByDefault ? $" and {IndexFields.Search.IsExcludedByDefault} eq false" : "";

            return filterString;
        }

        private static IList<string> GetOrderBy(V2SortBy sortBy)
        {
            IList<string> orderBy;
            switch (sortBy)
            {
                case V2SortBy.Popularity:
                    orderBy = null;
                    break;
                case V2SortBy.LastEditedDescending:
                    orderBy = LastEditedDescending;
                    break;
                case V2SortBy.PublishedDescending:
                    orderBy = PublishedDescending;
                    break;
                case V2SortBy.SortableTitleAsc:
                    orderBy = SortableTitleAscending;
                    break;
                case V2SortBy.SortableTitleDesc:
                    orderBy = SortableTitleDescending;
                    break;
                default:
                    throw new ArgumentException($"The provided {nameof(V2SortBy)} is not supported.", nameof(sortBy));
            }

            return orderBy;
        }
    }
}
