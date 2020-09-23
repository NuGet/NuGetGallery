// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Contains the parsed in-memory model of the user's query and any additional parameters used to construct the
    /// final Lucene query text.
    /// </summary>
    public class ParsedQuery
    {
        public ParsedQuery(Dictionary<QueryField, HashSet<string>> grouping, bool includeTestData)
        {
            Grouping = grouping ?? throw new ArgumentNullException(nameof(grouping));
            IncludeTestData = includeTestData;
        }

        public Dictionary<QueryField, HashSet<string>> Grouping { get; }
        public bool IncludeTestData { get; }
    }
}
