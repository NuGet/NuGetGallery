// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchConfiguration
    {
        public string SearchServiceName { get; set; }
        public string SearchServiceApiKey { get; set; }
        public string SearchIndexName { get; set; }
        public string HijackIndexName { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageContainer { get; set; }
        public string StoragePath { get; set; }

        public string NormalizeStoragePath()
        {
            var storagePath = StoragePath?.Trim('/') ?? string.Empty;
            if (storagePath.Length > 0)
            {
                storagePath = storagePath + "/";
            }

            return storagePath;
        }
    }
}
