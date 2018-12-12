// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchRequest
    {
        public int Skip { get; set; }
        public int Take { get; set; }
        public bool IncludePrerelease { get; set; }
        public bool IncludeSemVer2 { get; set; }
        public string Query { get; set; }
        public bool ShowDebug { get; set; }
    }
}
