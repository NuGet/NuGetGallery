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
    /// Provides operations for staging and retrieving private packages.
    /// </summary>
    public interface IPackageStagingService
    {
        /// <summary>
        /// Validates and stages a package on behalf of an authorized owner.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <param name="packageFile">The stream containing the package file.</param>
        /// <returns>The result of the staging operation.</returns>
        Task<PackageStagingResult> StagePackageAsync(User currentUser, IEnumerable<Scope> scopes, HttpContextBase httpContext, Stream packageFile);

        /// <summary>
        /// Gets an owner-visible staged package.
        /// </summary>
        /// <param name="currentUser">The user associated with the staging credential.</param>
        /// <param name="scopes">The scopes granted to the staging credential.</param>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The staged package status, or <see langword="null"/> when the package is not visible to the caller.</returns>
        PackageStagingStatus GetPackage(User currentUser, IEnumerable<Scope> scopes, string id, string version);
    }
}
