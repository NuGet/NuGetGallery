// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Starts validation for staged symbol packages.
    /// </summary>
    public interface IStagedSymbolPackageValidationMessageEmitter
    {
        /// <summary>
        /// Starts validation for the specified staged symbol package.
        /// </summary>
        /// <param name="stagedSymbolPackage">The staged symbol package to validate.</param>
        /// <returns>The status that should be applied to the staged symbol package.</returns>
        Task<StagedPackageStatus> StartValidationAsync(StagedSymbolPackage stagedSymbolPackage);
    }
}
