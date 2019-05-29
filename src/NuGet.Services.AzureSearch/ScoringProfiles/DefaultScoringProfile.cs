// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.AzureSearch.ScoringProfiles
{
    public static class DefaultScoringProfile
    {
        public const string Name = "nuget_scoring_profile";

        public static readonly ScoringProfile Instance = new ScoringProfile(
            Name,
            textWeights: new TextWeights
            {
                Weights = new Dictionary<string, double>
                {
                    // Exact match of the package id should be boosted the highest,
                    // followed by a tokenized match on the package id.
                    { IndexFields.PackageId, 10 },
                    { IndexFields.TokenizedPackageId, 5 },
                }
            },
            functions: new List<ScoringFunction>()
            {
                // Greatly boost results with high download counts. We score off the log of the download count
                // with linear interpolation so that the boost slows down at higher download counts. We cannot
                // use the raw download count with a log interpolation as that would result in a large boosting
                // range, which would need to be offset by an unmanageably high boosting factor.
                new MagnitudeScoringFunction(
                    fieldName: IndexFields.Search.LogBase2DownloadCount,
                    boost: 100.0,
                    parameters: new MagnitudeScoringParameters(
                        boostingRangeStart: 0,
                        boostingRangeEnd: Math.Log(999_999_999_999, 2),
                        shouldBoostBeyondRangeByConstant: true),
                    interpolation: ScoringFunctionInterpolation.Linear),

                // Boost results with a recent published date. We use a quadatric interpolation
                // so that the boost decreases faster as the publish date nears the end of the boost range.
                new FreshnessScoringFunction(
                    fieldName: IndexFields.Published,
                    boost: 5.0,
                    boostingDuration: TimeSpan.FromDays(365),
                    interpolation: ScoringFunctionInterpolation.Quadratic),
            },

            // The scores of each Scoring Function should be summed together before multiplying the base relevance scores.
            // See: https://stackoverflow.com/questions/41427940/how-do-scoring-profiles-generate-scores-in-azure-search
            functionAggregation: ScoringFunctionAggregation.Sum);
    }
}
