// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Support for NuGet style description analysis. This splits tokens
    /// on non alpha-numeric characters, splits tokens on camel casing,
    /// lower cases tokens, and then removes stopwords from tokens.
    /// </summary>
    public static class DescriptionAnalyzer
    {
        public const string Name = "nuget_description_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(Name, PackageIdCustomTokenizer.Name)
        {
            TokenFilters =
            {
                IdentifierCustomTokenFilter.Name,
                TokenFilterName.Lowercase,
                TokenFilterName.Stopwords,
                TruncateCustomTokenFilter.Name,
            }
        };
    }
}
