// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageDeprecationManagementService
    {
        /// <summary>
        /// Returns the versions of a package registration that could be an alternate version of another package's deprecation.
        /// </summary>
        /// <param name="id">The ID of the package registration to fetch alternates from.</param>
        IReadOnlyCollection<string> GetPossibleAlternatePackageVersions(
            string id);

        /// <summary>
        /// Updates the deprecation metadata of several packages with the same ID.
        /// </summary>
        /// <param name="currentUser">The user that is performing the update.</param>
        /// <param name="id">The ID of the package to update.</param>
        /// <param name="versions">The versions of the package to update.</param>
        /// <param name="isLegacy">Whether or not the packages are legacy.</param>
        /// <param name="hasCriticalBugs">Whether or not the packages have critical bugs.</param>
        /// <param name="isOther">Whether or not the packages have an unlisted reason for being deprecated.</param>
        /// <param name="alternatePackageId">An alternate package ID to use instead.</param>
        /// <param name="alternatePackageVersion">A version of <paramref name="alternatePackageId"/> to use instead.</param>
        /// <param name="message">A custom message to add to the deprecation.</param>
        /// <returns>
        /// <c>null</c> if there were no issues updating the deprecation.
        /// Otherwise, a <see cref="UpdateDeprecationError"/> that describes the problem that was encountered.
        /// </returns>
        Task<UpdateDeprecationError> UpdateDeprecation(
            User currentUser,
            string id,
            IEnumerable<string> versions,
            bool isLegacy = false,
            bool hasCriticalBugs = false,
            bool isOther = false,
            string alternatePackageId = null,
            string alternatePackageVersion = null,
            string message = null);
    }
}
