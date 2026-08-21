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
    /// Validates and stores private staged packages.
    /// </summary>
    public interface IPackageStagingUploadService
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
    }
}
