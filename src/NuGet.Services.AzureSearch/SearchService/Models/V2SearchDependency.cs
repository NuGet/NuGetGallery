// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchDependency
    {
        public string Id { get; set; }
        public string VersionSpec { get; set; }
        public string TargetFramework { get; set; }
    }
}
