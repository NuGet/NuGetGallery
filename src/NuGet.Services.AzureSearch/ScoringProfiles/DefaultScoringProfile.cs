// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search.Models;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.AzureSearch.ScoringProfiles
{
    public static class DefaultScoringProfile
    {
        public const string Name = "nuget_scoring_profile";

        private static readonly Lazy<Dictionary<string, string>> KnownSearchIndexFields = new Lazy<Dictionary<string, string>>(() =>
        {
            Dictionary<string, string> FieldValues(Type type)
            {
                return type
                    .GetFields()
                    .Where(f => f.IsStatic)
                    .Where(f => f.IsPublic)
                    .Where(f => f.FieldType == typeof(string))
                    .ToDictionary(
                        f => f.Name,
                        f => (string)f.GetValue(null));
            }

            var indexFields = FieldValues(typeof(IndexFields));
            var searchFields = FieldValues(typeof(IndexFields.Search));

            return indexFields.Concat(searchFields).ToDictionary(x => x.Key, x => x.Value);
        });

        public static ScoringProfile Create(AzureSearchScoringConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config.DownloadScoreBoost <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(config.DownloadScoreBoost));
            }

            if (config.PublishedFreshnessBoost <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(config.PublishedFreshnessBoost));
            }

            if (config.FieldWeights.Count != 0)
            {
                var unknownField = config
                    .FieldWeights
                    .Keys
                    .FirstOrDefault(f => !KnownSearchIndexFields.Value.Keys.Contains(f));

                if (unknownField != null)
                {
                    throw new ArgumentException(
                        $"Unknown field '{unknownField}' in {nameof(AzureSearchScoringConfiguration)}.{nameof(config.FieldWeights)}",
                        nameof(config));
                }
            }

            var fieldWeights = config
                .FieldWeights
                .ToDictionary(
                    g => KnownSearchIndexFields.Value[g.Key],
                    g => g.Value);

            return new ScoringProfile(
                Name,
                textWeights: new TextWeights(fieldWeights),
                functions: new List<ScoringFunction>()
                {
                    // Greatly boost results with high download counts. We score off the log of the download count
                    // with linear interpolation so that the boost slows down at higher download counts. We cannot
                    // use the raw download count with a log interpolation as that would result in a large boosting
                    // range, which would need to be offset by an unmanageably high boosting factor.
                    new MagnitudeScoringFunction(
                        fieldName: IndexFields.Search.DownloadScore,
                        boost: config.DownloadScoreBoost,
                        parameters: new MagnitudeScoringParameters(
                            boostingRangeStart: 0,
                            boostingRangeEnd: DocumentUtilities.GetDownloadScore(999_999_999_999),
                            shouldBoostBeyondRangeByConstant: true),
                        interpolation: ScoringFunctionInterpolation.Linear),

                    // Boost results with a recent published date. We use a quadatric interpolation
                    // so that the boost decreases faster as the publish date nears the end of the boost range.
                    new FreshnessScoringFunction(
                        fieldName: IndexFields.Published,
                        boost: config.PublishedFreshnessBoost,
                        boostingDuration: TimeSpan.FromDays(365),
                        interpolation: ScoringFunctionInterpolation.Quadratic),
                },

                // The scores of each Scoring Function should be summed together before multiplying the base relevance scores.
                // See: https://stackoverflow.com/questions/41427940/how-do-scoring-profiles-generate-scores-in-azure-search
                functionAggregation: ScoringFunctionAggregation.Sum);
        }
    }
}
