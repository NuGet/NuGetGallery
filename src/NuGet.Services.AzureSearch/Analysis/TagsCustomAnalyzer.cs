// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Support for tag case-insensitive exact matching on a field with trimming
    /// in an Azure Search index.
    ///
    /// Note: will not split on special characters like -,_,$,etc. This is important
    /// and allows developers to hyphenate their tags. It will trim excess whitespace
    /// from the end of each tag.
    ///
    /// Tokenization will also exclude duplicates from the indexing process.
    /// </summary>
    public static class TagsCustomAnalyzer
    {
        public const string Name = "nuget_tags_analyzer";

        public static readonly CustomAnalyzer Instance = new CustomAnalyzer(Name, LexicalTokenizerName.Keyword)
        {
            TokenFilters =
            {
                TokenFilterName.Lowercase,
                TokenFilterName.Trim,
                TokenFilterName.Unique,
            }
        };
    }
}