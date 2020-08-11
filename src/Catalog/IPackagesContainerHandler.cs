// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A handler that is run on packages in the packages container.
    /// </summary>
    public interface IPackagesContainerHandler
    {
        /// <summary>
        /// Handle a package in the packages container.
        /// </summary>
        /// <param name="packageEntry">The package's catalog index entry.</param>
        /// <param name="blob">The package's blob in the packages container.</param>
        /// <returns>A task that completes once the package has been handled.</returns>
        Task ProcessPackageAsync(CatalogIndexEntry packageEntry, ICloudBlockBlob blob);
    }
}
