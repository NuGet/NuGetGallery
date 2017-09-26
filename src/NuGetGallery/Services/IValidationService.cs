// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// The service for interacting with the asynchronous validation pipeline.
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Starts the asynchronous validation for the provided new package and puts the package in the correct
        /// <see cref="Package.PackageStatusKey"/>. The commit to the database is the responsibility of the caller.
        /// </summary>
        /// <param name="package">The package to start validation for.</param>
        Task StartValidationAsync(Package package);

        /// <summary>
        /// Starts the asynchronous validation for the provided new package but does not change the package's
        /// <see cref="Package.PackageStatusKey"/>.
        /// </summary>
        /// <param name="package">The package to start validation for.</param>
        Task RevalidateAsync(Package package);
    }
}