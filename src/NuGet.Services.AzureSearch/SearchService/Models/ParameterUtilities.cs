// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.SearchService
{
    public static class ParameterUtilities
    {
        private static readonly NuGetVersion SemVer2Level = new NuGetVersion("2.0.0");

        private static readonly IReadOnlyDictionary<string, V2SortBy> SortBy = new Dictionary<string, V2SortBy>(StringComparer.OrdinalIgnoreCase)
        {
            { "relevance", V2SortBy.Popularity },
            { "lastEdited", V2SortBy.LastEditedDesc },
            { "published", V2SortBy.PublishedDesc },
            { "title-asc", V2SortBy.SortableTitleAsc },
            { "title-desc", V2SortBy.SortableTitleDesc },
            { "created-asc", V2SortBy.CreatedAsc },
            { "created-desc", V2SortBy.CreatedDesc },
            { "totalDownloads-asc", V2SortBy.TotalDownloadsAsc },
            { "totalDownloads-desc", V2SortBy.TotalDownloadsDesc },
        };

        public static V2SortBy ParseV2SortBy(string sortBy)
        {
            if (sortBy == null || !SortBy.TryGetValue(sortBy, out var parsedSortBy))
            {
                parsedSortBy = V2SortBy.Popularity;
            }

            return parsedSortBy;
        }

        public static bool ParseIncludeSemVer2(string semVerLevel)
        {
            if (!NuGetVersion.TryParse(semVerLevel, out var semVerLevelVersion))
            {
                return false;
            }
            else
            {
                return semVerLevelVersion >= SemVer2Level;
            }
        }
    }
}
