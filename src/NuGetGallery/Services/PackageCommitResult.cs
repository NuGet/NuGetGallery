// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGetGallery
{
    /// <summary>
    /// Non-exceptional results of calling <see cref="IPackageUploadService.CommitPackageAsync(Package, Stream)"/>.
    /// </summary>
    public enum PackageCommitResult
    {
        /// <summary>
        /// The package was successfully committed to the package file storage and to the database.
        /// </summary>
        Success,

        /// <summary>
        /// The package file conflicts with an existing package file. The package was not committed to the database.
        /// </summary>
        Conflict,
    }
}