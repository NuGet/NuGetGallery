// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// An interface which allows custom processing of catalog leaves. This interface should be implemented when the
    /// catalog leaf documents need to be downloaded and processed in chronological order.
    /// </summary>
    public interface ICatalogLeafProcessor
    {
        /// <summary>
        /// Process a catalog leaf containing package details. This method should return false or throw an exception
        /// if the catalog leaf cannot be processed. In this case, the <see cref="CatalogProcessor" /> will stop
        /// processing items. Note that the same package ID/version combination can be passed to this multiple times,
        /// for example due to an edit in the package metadata or due to a transient error and retry on the part of the
        /// <see cref="CatalogProcessor" />.
        /// </summary>
        /// <param name="leaf">The leaf document.</param>
        /// <returns>True, if the leaf was successfully processed. False, otherwise.</returns>
        Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf);

        /// <summary>
        /// Process a catalog leaf containing a package delete. This method should return false or throw an exception
        /// if the catalog leaf cannot be processed. In this case, the <see cref="CatalogProcessor" /> will stop
        /// processing items. Note that the same package ID/version combination can be passed to this multiple times,
        /// for example due to a package being deleted again due to a transient error and retry on the part of the
        /// <see cref="CatalogProcessor" />.
        /// </summary>
        /// <param name="leaf">The leaf document.</param>
        /// <returns>True, if the leaf was successfully processed. False, otherwise.</returns>
        Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf);
    }
}
