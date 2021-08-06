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
        /// Updates the listed status on a batch of packages. All of the packages must be related to the same package registration.
        /// Packages that are deleted or have failed validation are not allowed. Packages that already have a matching listed state
        /// will not be skipped, to enable reflow of listed status.
        /// </summary>
        /// <param name="packages">The packages to update.</param>
        /// <param name="listed">True to make the packages listed, false to make the packages unlisted.</param>
        Task UpdateListedInBulkAsync(IReadOnlyList<Package> packages, bool listed);

        /// <summary>
        /// Marks <paramref name="package"/> as listed.
        /// </summary>
        /// <param name="package">The package to list.</param>
        /// <param name="commitChanges">Whether or not changes should be committed.</param>
        /// <param name="updateIndex">If true, <see cref="IIndexingService.UpdatePackage(Package)"/> will be called.</param>
        Task MarkPackageListedAsync(Package package, bool commitChanges = true, bool updateIndex = true);

        /// <summary>
        /// Marks the packages in <paramref name="packages"/> as updated.
        /// </summary>
        /// <param name="updateIndex">If true, <see cref="IIndexingService.UpdatePackage(Package)"/> will be called.</param>
        Task UpdatePackagesAsync(IReadOnlyList<Package> packages, bool updateIndex = true);
    }
}
