// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;
using NuGetGallery;

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

        private static readonly IReadOnlyDictionary<string, V2FrameworkFilterMode> FrameworkFilterMode = new Dictionary<string, V2FrameworkFilterMode>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", V2FrameworkFilterMode.All },
            { "any", V2FrameworkFilterMode.Any },
        };

        private static readonly HashSet<string> FrameworkGenerationIdentifiers = new HashSet<string>{
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard
        };

        public static V2SortBy ParseV2SortBy(string sortBy)
        {
            if (sortBy == null || !SortBy.TryGetValue(sortBy, out var parsedSortBy))
            {
                parsedSortBy = V2SortBy.Popularity;
            }

            return parsedSortBy;
        }

        public static V2FrameworkFilterMode ParseV2FrameworkFilterMode(string frameworkFilterMode)
        {
            // if the input parameter is null or an unexpected value, default to 'All'
            if (frameworkFilterMode == null || !FrameworkFilterMode.TryGetValue(frameworkFilterMode, out var parsedSelector))
            {
                parsedSelector = V2FrameworkFilterMode.All;
            }

            return parsedSelector;
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

        public static IReadOnlyList<string> ParseFrameworks(string frameworks)
        {
            var frameworkList = frameworks == null
                                    ? (IReadOnlyList<string>)Array.Empty<string>()
                                    : frameworks
                                        .Split(',')
                                        .Select(f => f.ToLowerInvariant().Trim())
                                        .Where(f => f != String.Empty);

            var result = new List<string>();
            foreach (var framework in frameworkList)
            {
                if (FrameworkGenerationIdentifiers.Contains(framework))
                {
                    result.Add(framework);
                }
                else
                {
                    throw new InvalidSearchRequestException($"The provided Framework is not supported. (Parameter '{framework}')");
                }
            }

            return result
                    .Distinct()
                    .ToList();
        }

        public static IReadOnlyList<string> ParseTfms(string tfms)
        {
            var tfmList = tfms == null
                            ? (IReadOnlyList<string>)Array.Empty<string>()
                            : tfms
                                .Split(',')
                                .Select(f => f.Trim())
                                .Where(f => f != String.Empty);

            var result = new List<string>();
            foreach (var tfm in tfmList)
            {
                var f = NuGetFramework.Parse(tfm);
                if (f.IsSpecificFramework && !f.IsPCL)
                {
                    result.Add(f.GetShortFolderName());
                }
                else
                {
                    throw new InvalidSearchRequestException($"The provided TFM is not supported. (Parameter '{tfm}')");
                }
            }

            return result
                    .Distinct()
                    .ToList();
        }
    }
}
