// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    /// <summary>
    /// Settings used at development time that should not be used in production environments.
    /// </summary>
    public class Db2AzureSearchDevelopmentConfiguration : AzureSearchJobDevelopmentConfiguration
    {
        /// <summary>
        /// If true, deletes the existing Azure Storage containers and Azure Search indexes.
        /// This should be false on production environments.
        /// </summary>
        public bool ReplaceContainersAndIndexes { get; set; }

        /// <summary>
        /// Db2AzureSearch skips packages whose ID start with these prefixes.
        /// This is case insensitive. This should be empty on production environments.
        /// </summary>
        public IReadOnlyList<string> SkipPackagePrefixes { get; set; }

        /// <summary>
        /// The Kusto connection string to use for building the Azure Search index from NuGet.Insights Kusto data.
        /// </summary>
        public string KustoConnectionString { get; set; }

        /// <summary>
        /// The Kusto database name to use for building the Azure Search index from NuGet.Insights Kusto data.
        /// </summary>
        public string KustoDatabaseName { get; set; }

        /// <summary>
        /// This is the table name pattern to use for NuGet.Insights table names. It is a format string for 
        /// <see cref="System.String.Format(string, object)"/> where the input is the default NuGet.Insights table name.
        /// </summary>
        public string KustoTableNameFormat { get; set; } = "{0}";

        /// <summary>
        /// If set to a number greater than 0, this will limit the number of packages to index from Kusto, ordered by total download count.
        /// </summary>
        public int KustoTopPackageCount { get; set; }

        /// <summary>
        /// If <see cref="KustoTopPackageCount"/> is set, this will limit the packages fetched from Kusto to only the latest versions.
        /// </summary>
        public bool KustoOnlyLatestPackages { get; set; }
    }
}
