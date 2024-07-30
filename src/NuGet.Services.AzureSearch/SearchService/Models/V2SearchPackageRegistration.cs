// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchPackageRegistration
    {
        public string Id { get; set; }
        public long DownloadCount { get; set; }
        public bool Verified { get; set; }
        public string[] Owners { get; set; }
        public string[] PopularityTransfers { get; set; }
    }
}
