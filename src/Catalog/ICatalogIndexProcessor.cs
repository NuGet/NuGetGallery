// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A processor that runs on entries found on a catalog page.
    /// See: https://docs.microsoft.com/en-us/nuget/api/catalog-resource#catalog-page
    /// </summary>
    public interface ICatalogIndexProcessor
    {
        /// <summary>
        /// Process a single entry from a catalog page.
        /// </summary>
        /// <param name="catalogEntry">The catalog index entry that should be processed.</param>
        /// <returns>A task that completes once the entry has been processed.</returns>
        Task ProcessCatalogIndexEntryAsync(CatalogIndexEntry catalogEntry);
    }
}
