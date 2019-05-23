// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.AzureSearch.ScoringProfiles
{
    public class DownloadCountBoosterProfile
    {
        public const string Name = "download_count_scoring_profile";

        public static readonly ScoringProfile Instance = new ScoringProfile(
            Name,
            textWeights: new TextWeights
            {
                Weights = new Dictionary<string, double>
                {
                    { IndexFields.PackageId, 10 },              // Exact match of the package id should be boosted higher.
                    { IndexFields.TokenizedPackageId, 5 }
                }
            },
            functions: new List<ScoringFunction>()
            {
                new MagnitudeScoringFunction(
                    fieldName: IndexFields.Search.TotalDownloadCount,
                    boost: 100.0,
                    parameters: new MagnitudeScoringParameters(2000, double.MaxValue, shouldBoostBeyondRangeByConstant: true),
                    interpolation: ScoringFunctionInterpolation.Logarithmic)
            },
            functionAggregation: ScoringFunctionAggregation.Sum);
    }
}
