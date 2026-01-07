// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Symbol Package business logic that is needed in multiple components (not just the Gallery process).
    /// </summary>
    public interface ICoreSymbolPackageService
    {
        /// <summary>
        /// Gets all the symbol packages associated with the Package ID and version
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The list of <see cref="SymbolPackage"/> associated with this package</returns>
        IEnumerable<SymbolPackage> FindSymbolPackagesByIdAndVersion(string id, string version);

        /// <summary>
        /// Update the status of the symbol package.
        /// </summary>
        /// <param name="symbolPackage">The symbol package to update the status of.</param>
        /// <param name="status">Enum value for <see cref="PackageStatus"/></param>
        /// <param name="commitChanges">Whether or not to commit the changes to the entity context</param>
        /// <returns>Awaitable task</returns>
        Task UpdateStatusAsync(SymbolPackage symbolPackage, PackageStatus status, bool commitChanges = true);
    }
}