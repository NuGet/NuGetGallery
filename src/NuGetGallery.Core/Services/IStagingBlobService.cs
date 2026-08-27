// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// Stores private package files used by package staging.
    /// </summary>
    public interface IStagingBlobService
    {
        /// <summary>
        /// Saves a package file to a new immutable path in staging storage.
        /// </summary>
        /// <param name="packageId">The package ID.</param>
        /// <param name="normalizedVersion">The normalized package version.</param>
        /// <param name="packageFile">The stream containing the package file.</param>
        /// <returns>A reference describing the saved package file.</returns>
        Task<StagingFileReference> SavePackageFileAsync(string packageId, string normalizedVersion, Stream packageFile);

        /// <summary>
        /// Gets a temporary read URI for an unchanged staged package.
        /// </summary>
        /// <param name="packagePath">The package path in private staging storage.</param>
        /// <param name="packageETag">The expected source ETag.</param>
        /// <returns>A temporary read URI for the staged package.</returns>
        Task<Uri> GetPackageReadUriAsync(string packagePath, string packageETag);
    }
}
