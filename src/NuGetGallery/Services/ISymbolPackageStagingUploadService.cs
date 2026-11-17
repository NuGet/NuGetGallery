// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Validates and stores private staged symbol packages.
    /// </summary>
    public interface ISymbolPackageStagingUploadService
    {
        /// <summary>
        /// Stages or replaces a symbol package for an available or same-owner staged parent package.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <param name="symbolPackageFile">The stream containing the symbol package.</param>
        /// <param name="groupId">The optional group for the shared package and symbol identity.</param>
        /// <returns>The result of the staging operation.</returns>
        Task<PackageStagingResult> StageSymbolPackageAsync(User currentUser, IReadOnlyCollection<Scope> scopes, HttpContextBase httpContext, Stream symbolPackageFile, string groupId = null);

        /// <summary>
        /// Replaces an authorized current staged symbol package with a fresh immutable validation attempt.
        /// </summary>
        /// <param name="currentUser">The user replacing the symbol package.</param>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <param name="stagedSymbolPackage">The authorized current symbol attempt.</param>
        /// <param name="symbolPackageFile">The replacement symbol package stream.</param>
        /// <returns>The result of the replacement operation.</returns>
        Task<PackageStagingResult> ReplaceSymbolPackageAsync(
            User currentUser,
            HttpContextBase httpContext,
            StagedSymbolPackage stagedSymbolPackage,
            Stream symbolPackageFile);

        /// <summary>
        /// Gets the owner-visible status for the current staged symbol package.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The staged symbol status, or <see langword="null"/> when it is not visible to the caller.</returns>
        SymbolPackageStagingStatus GetStatus(User currentUser, IReadOnlyCollection<Scope> scopes, string id, string version);
    }
}
