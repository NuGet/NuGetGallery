// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchConfiguration
    {
        public int DatabaseBatchSize { get; set; }
        public int AzureSearchBatchSize { get; set; }
        public int WorkerCount { get; set; }
        public string SearchServiceName { get; set; }
        public string SearchServiceApiKey { get; set; }
        public string SearchIndexName { get; set; }
        public string HijackIndexName { get; set; }
        public bool ReplaceIndexes { get; set; }
    }
}
