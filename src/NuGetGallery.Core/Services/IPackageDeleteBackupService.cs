// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// Stores and retrieves deletion backup JSON alongside package archives in the private package backup container.
    /// Each deletion has a separate JSON document. Does not store package binaries or perform undo.
    /// </summary>
    public interface IPackageDeleteBackupService
    {
        /// <summary>
        /// Saves pre-delete state, binary backup hashes, external README text and incoming deprecation keys
        /// as JSON before deletion cleanup.
        /// </summary>
        /// <param name="backup">The data needed to undo this package-version deletion.</param>
        Task SaveAsync(PackageDeleteBackup backup);

        /// <summary>
        /// Loads deletion backup JSON from the package ID and normalized version prefix,
        /// validating its structure, identity and keys.
        /// </summary>
        /// <returns>The saved backup, or null if no JSON backup exists.</returns>
        /// <remarks>
        /// Does not check undo eligibility or verify package binary contents. Reading leaves the backup unchanged.
        /// </remarks>
        Task<PackageDeleteBackup> GetAsync(string packageId, string normalizedVersion, int packageDeleteKey, int packageKey);

        /// <summary>
        /// Removes JSON written by a failed deletion attempt. Does not remove archive backups.
        /// </summary>
        Task DeleteAsync(string packageId, string normalizedVersion, int packageDeleteKey, int packageKey);
    }
}