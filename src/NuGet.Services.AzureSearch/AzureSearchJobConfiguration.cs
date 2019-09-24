// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchJobConfiguration : AzureSearchConfiguration
    {
        public int AzureSearchBatchSize { get; set; } = 1000;

        /// <summary>
        /// The definition of batches is defined by the job that uses this configuration value, but in general this
        /// property is used to control how many "workers" are allowed to work in parallel producing, and perhaps also
        /// pushing, document changes for Azure Search.
        /// </summary>
        public int MaxConcurrentBatches { get; set; } = 4;

        /// <summary>
        /// The maximum number of threads that should write version lists in parallel. This primary use for this
        /// property is the <see cref="BatchPusher"/> implementation that updates version lists after all documents
        /// for a specific package ID have been pushed to Azure Search. The specific semantics of this property vary
        /// on a per-job basis. In some cases this property may be used within a batch
        /// (i.e. <see cref="MaxConcurrentBatches"/>) so the actual maximum degree of parallelism for a single process
        /// may be a multiple of this property.
        /// </summary>
        public int MaxConcurrentVersionListWriters { get; set; } = 8;

        public string GalleryBaseUrl { get; set; }

        public AzureSearchScoringConfiguration Scoring { get; set; }

        public Uri ParseGalleryBaseUrl()
        {
            return new Uri(GalleryBaseUrl, UriKind.Absolute);
        }
    }
}
