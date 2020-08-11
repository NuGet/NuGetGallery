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
    }
}
