// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation.Issues;

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

        /// <summary>
        /// Get the package's validation issues from the latest validation.
        /// </summary>
        /// <param name="package">The package whose validation issues should be fetched.</param>
        /// <returns>The validation issues encountered in the latest validation.</returns>
        IReadOnlyList<ValidationIssue> GetLatestValidationIssues(Package package);
    }
}