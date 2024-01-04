// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchRequest : SearchRequest
    {
        public bool IgnoreFilter { get; set; }
        public bool CountOnly { get; set; }
        public V2SortBy SortBy { get; set; }
        public bool LuceneQuery { get; set; }
        public string PackageType { get; set; }
        public IReadOnlyList<string> Frameworks { get; set; }
        public IReadOnlyList<string> Tfms { get; set; }
        public bool IncludeComputedFrameworks { get; set; }
        public V2FrameworkFilterMode FrameworkFilterMode { get; set; }
    }
}
