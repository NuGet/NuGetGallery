// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Support for case-insensitive exact matching on a field
    /// in an Azure Search index.
    /// </summary>
    public static class ExactMatchCustomAnalyzer
    {
        public const string Name = "nuget_exact_match_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(Name, LexicalTokenizerName.Keyword)
        {
            TokenFilters =
            {
                TokenFilterName.Lowercase
            }
        };
    }
}
