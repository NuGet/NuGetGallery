// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Protocol.Catalog
{
    public interface ICatalogClient
    {
        /// <summary>
        /// Get the catalog index at the provided URL. The catalog index URL should be discovered from the
        /// service index.
        /// </summary>
        /// <param name="indexUrl">The catalog index URL.</param>
        /// <returns>The catalog index.</returns>
        Task<CatalogIndex> GetIndexAsync(string indexUrl);

        /// <summary>
        /// Get the catalog page at the provided URL. The catalog page URL should be discovered from the catalog
        /// index.
        /// </summary>
        /// <param name="pageUrl">The catalog page URL.</param>
        /// <returns>The catalog page.</returns>
        Task<CatalogPage> GetPageAsync(string pageUrl);

        /// <summary>
        /// Gets the catalog leaf at the provided URL. The catalog leaf URL should be discovered from a catalog page.
        /// The type of the catalog leaf is automatically determined from the fetched document.
        /// </summary>
        /// <param name="leafUrl">The catalog leaf URL.</param>
        /// <returns>The catalog leaf.</returns>
        Task<CatalogLeaf> GetLeafAsync(string leafUrl);
        
        /// <summary>
        /// Gets the catalog leaf at the provided URL. The catalog leaf URL should be discovered from a catalog page.
        /// The type of the catalog leaf must be a package delete. If the actual document is not a package delete, an
        /// exception is thrown.
        /// </summary>
        /// <param name="leafUrl">The catalog leaf URL.</param>
        /// <exception cref="ArgumentException">Thrown if the actual document is not a package delete.</exception>
        /// <returns>The catalog leaf.</returns>
        Task<PackageDeleteCatalogLeaf> GetPackageDeleteLeafAsync(string leafUrl);

        /// <summary>
        /// Gets the catalog leaf at the provided URL. The catalog leaf URL should be discovered from a catalog page.
        /// The type of the catalog leaf must be package details. If the actual document is not package details, an
        /// exception is thrown.
        /// </summary>
        /// <param name="leafUrl">The catalog leaf URL.</param>
        /// <exception cref="ArgumentException">Thrown if the actual document is not package details.</exception>
        /// <returns>The catalog leaf.</returns>
        Task<PackageDetailsCatalogLeaf> GetPackageDetailsLeafAsync(string leafUrl);
    }
}