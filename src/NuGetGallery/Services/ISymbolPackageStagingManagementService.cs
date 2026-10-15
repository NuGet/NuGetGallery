// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Retrieves and manages private staged symbols for their owners.
    /// </summary>
    public interface ISymbolPackageStagingManagementService
    {
        /// <summary>
        /// Gets current staged symbols visible to the signed-in user.
        /// </summary>
        IReadOnlyList<StagedSymbolPackage> GetStagedSymbolPackages(User currentUser);

        /// <summary>
        /// Gets current staged symbols visible to the user in the specified owner's group or Ungrouped.
        /// </summary>
        /// <param name="currentUser">The user requesting the staged symbols.</param>
        /// <param name="ownerKey">The staging owner's key.</param>
        /// <param name="groupKey">The group key, or <see langword="null"/> for Ungrouped.</param>
        /// <returns>The owner-visible staged symbols in the requested scope.</returns>
        IReadOnlyList<StagedSymbolPackage> GetStagedSymbolPackages(User currentUser, int ownerKey, int? groupKey);

        /// <summary>
        /// Finds a current staged symbol attempt. The caller must authorize access before using it.
        /// </summary>
        StagedSymbolPackage FindCurrentStagedSymbolPackage(string id, string version);

        /// <summary>
        /// Opens the immutable uploaded content of an authorized staged symbol attempt.
        /// </summary>
        Task<Stream> OpenPackageContentAsync(StagedSymbolPackage stagedSymbolPackage);

        /// <summary>
        /// Deletes an authorized staged symbol attempt without changing its public parent or public symbols.
        /// Returns false when the staging state changed or promotion prevents deletion.
        /// </summary>
        Task<bool> DeletePackageAsync(StagedSymbolPackage stagedSymbolPackage);
    }
}
