// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery
{
    /// <summary>
    /// The service for interacting with the asynchronous validation pipeline.
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Updates the package with the expected <see cref="PackageStatus"/> that the package will
        /// have after starting the validation.
        /// The caller must also call <see cref="IValidationService.StartValidationAsync(Package)"/>
        /// at later time.
        /// </summary>
        /// <param name="package">package to update</param>
        Task UpdatePackageAsync(Package package);

        /// <summary>
        /// Updates the symbol package with the expected <see cref="PackageStatus"/> that the package will
        /// have after starting the validation.
        /// The caller must also call <see cref="IValidationService.StartValidationAsync(Package)"/>
        /// at later time.
        /// </summary>
        /// <param name="package">package to update</param>
        Task UpdatePackageAsync(SymbolPackage symbolPackage);

        /// <summary>
        /// Starts the asynchronous validation for the provided new package and puts the package in the correct
        /// <see cref="Package.PackageStatusKey"/>. The commit to the database is the responsibility of the caller.
        /// </summary>
        /// <param name="package">The package to start validation for.</param>
        Task StartValidationAsync(Package package);

        /// <summary>
        /// Starts the asynchronous validation for the provided new symbol package and puts the symbol package in the correct
        /// <see cref="Package.PackageStatusKey"/>. The commit to the database is the responsibility of the caller.
        /// </summary>
        /// <param name="symbolPackage">The symbol package to start validation for.</param>
        Task StartValidationAsync(SymbolPackage symbolPackage);

        /// <summary>
        /// Starts the asynchronous validation for the provided new package but does not change the package's
        /// <see cref="Package.PackageStatusKey"/>.
        /// </summary>
        /// <param name="package">The package to start validation for.</param>
        Task RevalidateAsync(Package package);

        /// <summary>
        /// Starts the asynchronous validation for the provided new symbols package but does not change the symbol package's
        /// <see cref="Package.PackageStatusKey"/>.
        /// </summary>
        /// <param name="symbolPackage">The symbols package to start validation for.</param>
        Task RevalidateAsync(SymbolPackage symbolPackage);

        /// <summary>
        /// Whether the package's validation time exceeds the expected validation time.
        /// </summary>
        /// <param name="package">The package whose validation time should be inspected.</param>
        /// <returns>Whether the package's validation time exceeds the expected validation time.</returns>
        bool IsValidatingTooLong(Package package);

        /// <summary>
        /// Get the package's validation issues from the latest validation.
        /// </summary>
        /// <param name="package">The package whose validation issues should be fetched.</param>
        /// <returns>The validation issues encountered in the latest validation.</returns>
        IReadOnlyList<ValidationIssue> GetLatestPackageValidationIssues(Package package);

        /// <summary>
        /// Get the symbol package's validation issues from the latest validation.
        /// </summary>
        /// <param name="symbolPackage">The symbol package whose validation issues should be fetched.</param>
        /// <returns>The validation issues encountered in the latest validation.</returns>
        IReadOnlyList<ValidationIssue> GetLatestPackageValidationIssues(SymbolPackage symbolPackage);
    }
}