// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchConfiguration : AzureSearchJobConfiguration, IAuxiliaryDataStorageConfiguration
    {
        public int DatabaseBatchSize { get; set; } = 10000;
        public string CatalogIndexUrl { get; set; }
        public string AuxiliaryDataStorageConnectionString { get; set; }
        public string AuxiliaryDataStorageContainer { get; set; }
        public string AuxiliaryDataStorageDownloadsPath { get; set; }
        public string AuxiliaryDataStorageExcludedPackagesPath { get; set; }
        public string AuxiliaryDataStorageVerifiedPackagesPath { get; set; }
        public string DownloadsV1JsonUrl { get; set; }

        /// <summary>
        /// Toggles the usage of <see cref="ClassicSimilarityHandler"/>. This allows the creation of a search index
        /// with "classic" similarity. By default, indexes created with the new SDK use "BM25" similarity with yields
        /// slightly different search relevance.
        /// </summary>
        public bool UseClassicSimilarity { get; set; }
    }
}
