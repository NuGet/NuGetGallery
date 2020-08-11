// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchRequest : SearchRequest
    {
        public bool IgnoreFilter { get; set; }
        public bool CountOnly { get; set; }
        public V2SortBy SortBy { get; set; }
        public bool LuceneQuery { get; set; }
        public string PackageType { get; set; }
    }
}
