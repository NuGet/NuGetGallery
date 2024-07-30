// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchText
    {
        public SearchText(string value, bool isDefaultSearch)
        {
            Value = value;
            IsDefaultSearch = isDefaultSearch;
        }

        /// <summary>
        /// The search text, which is a Lucene expression.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Whether or not the search text represents a default search without any user provided terms.
        /// </summary>
        public bool IsDefaultSearch { get; }
    }
}