// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Starts validation for staged packages.
    /// </summary>
    public interface IStagedPackageValidationMessageEmitter
    {
        /// <summary>
        /// Starts validation for the specified staged package.
        /// </summary>
        /// <param name="stagedPackage">The staged package to validate.</param>
        /// <returns>Whether asynchronous validation was started.</returns>
        Task<bool> StartValidationAsync(StagedPackage stagedPackage);
    }
}
