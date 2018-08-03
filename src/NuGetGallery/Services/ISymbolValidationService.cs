// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery
{
    /// <summary>
    /// The service for interacting with the asynchronous validation pipeline for symbol service.
    /// </summary>
    public interface ISymbolValidationService
    {
        /// <summary>
        /// Starts the asynchronous validation for the provided new package and puts the package in the correct
        /// <see cref="SymbolPackage.StatusKey"/>. The commit to the database is the responsibility of the caller.
        /// </summary>
        /// <param name="symbolPackage">The symbol package to start validation for.</param>
        Task StartValidationAsync(SymbolPackage symbolPackage);

        /// <summary>
        /// Starts the asynchronous validation for the provided new symbol package but does not change the symbol package's
        /// <see cref="SymbolPackage.StatusKey"/>.
        /// </summary>
        /// <param name="symbolPackage">The package to start validation for.</param>
        Task RevalidateAsync(SymbolPackage symbolPackage);

        /// <summary>
        /// Whether the package's validation time exceeds the expected validation time.
        /// </summary>
        /// <param name="symbolPackage">The package whose validation time should be inspected.</param>
        /// <returns>Whether the package's validation time exceeds the expected validation time.</returns>
        bool IsValidatingTooLong(SymbolPackage symbolPackage);

        /// <summary>
        /// Get the package's validation issues from the latest validation.
        /// </summary>
        /// <param name="symbolPackage">The package whose validation issues should be fetched.</param>
        /// <returns>The validation issues encountered in the latest validation.</returns>
        IReadOnlyList<ValidationIssue> GetLatestValidationIssues(SymbolPackage symbolPackage);
    }
}