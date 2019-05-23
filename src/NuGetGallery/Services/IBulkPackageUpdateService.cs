// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IBulkPackageUpdateService
    {
        /// <summary>
        /// Updates the <see cref="Package.LastEdited"/> and <see cref="Package.LastUpdated"/> of each package in <paramref name="packages"/>.
        /// If <paramref name="setListed"/> is provided, updates the <see cref="Package.Listed"/> of each package as well.
        /// </summary>
        /// <remarks>
        /// If this method is not called from within a transaction, it may perform two separate commits.
        /// The caller should guarantee this method is called within a transaction to guarantee a single commit is performed.
        /// </remarks>
        Task UpdatePackagesAsync(IEnumerable<Package> packages, bool? setListed = null);
    }
}
