// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackageUpdateService
    {
        /// <summary>
        /// Marks <paramref name="package"/> as unlisted.
        /// </summary>
        /// <param name="package">The package to unlist.</param>
        /// <param name="commitChanges">Whether or not changes should be committed.</param>
        /// <param name="updateIndex">If true, <see cref="IIndexingService.UpdatePackage(Package)"/> will be called.</param>
        Task MarkPackageUnlistedAsync(Package package, bool commitChanges = true, bool updateIndex = true);

        /// <summary>
        /// Marks <paramref name="package"/> as listed.
        /// </summary>
        /// <param name="package">The package to list.</param>
        /// <param name="commitChanges">Whether or not changes should be committed.</param>
        /// <param name="updateIndex">If true, <see cref="IIndexingService.UpdatePackage(Package)"/> will be called.</param>
        Task MarkPackageListedAsync(Package package, bool commitChanges = true, bool updateIndex = true);

        /// <summary>
        /// Marks the packages in <paramref name="packages"/> as updated in a transaction.
        /// This method will commit the transaction.
        /// </summary>
        /// <param name="packages">
        /// The packages to mark as updated. All packages must have the same <see cref="PackageRegistration"/>.
        /// </param>
        /// <param name="listed">
        /// If true, all packages will be listed. 
        /// If false, all packages will be unlisted. 
        /// If null, the listing state of the packages will not be changed.
        /// </param>
        /// <param name="transaction">The transaction that this operation should run in.</param>
        /// <param name="updateIndex">If true, <see cref="IIndexingService.UpdatePackage(Package)"/> will be called.</param>
        Task UpdatePackagesAsync(IReadOnlyList<Package> packages, bool? listed, IDbContextTransaction transaction, bool updateIndex = true);
    }
}
