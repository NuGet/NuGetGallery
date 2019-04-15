// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchConfiguration : AzureSearchJobConfiguration
    {
        public int DatabaseBatchSize { get; set; }
        public bool ReplaceContainersAndIndexes { get; set; }
        public string CatalogIndexUrl { get; set; }
    }
}
