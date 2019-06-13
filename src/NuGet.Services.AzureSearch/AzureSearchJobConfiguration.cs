// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchJobConfiguration : AzureSearchConfiguration
    {
        public int AzureSearchBatchSize { get; set; }
        public int MaxConcurrentBatches { get; set; }
        public int MaxConcurrentVersionListWriters { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageContainer { get; set; }
        public string StoragePath { get; set; }
        public string GalleryBaseUrl { get; set; }

        public AzureSearchScoringConfiguration Scoring { get; set; }

        public Uri ParseGalleryBaseUrl()
        {
            return new Uri(GalleryBaseUrl, UriKind.Absolute);
        }

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
