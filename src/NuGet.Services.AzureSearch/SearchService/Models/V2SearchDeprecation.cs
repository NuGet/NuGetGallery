// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchDeprecation
    {
        public V2SearchAlternatePackage AlternatePackage { get; set; }

        public string Message { get; set; }

        public string[] Reasons { get; set; }
    }

    public class V2SearchAlternatePackage
    {
        public string Id { get; set; }

        public string Range { get; set; }
    }
}
