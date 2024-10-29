// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    /// <summary>
    /// Business logic related to <see cref="SymbolPackage"/>. This logic is only used by the NuGet Gallery,
    /// as opposed to the <see cref="CoreSymbolPackageService"/> which is intended for other components.
    /// </summary>
    public interface ISymbolPackageService : ICoreSymbolPackageService
    {
        /// <summary>
        /// Populate the related database tables to create the specified symbol package. It is the caller's responsibility to commit
        /// the changes.
        /// </summary>
        /// <remarks>
        /// This method doesn't upload the package binary to the blob storage. The caller must do it after this call.
        /// </remarks>
        /// <param name="nugetPackage">The nuget package for which symbol is to be created.</param>
        /// <param name="symbolPackageStreamMetadata">The symbol package stream's metadata.</param>
        /// <returns>The created symbol package entity.</returns>
        SymbolPackage CreateSymbolPackage(Package nugetPackage, PackageStreamMetadata symbolPackageStreamMetadata);

        Task EnsureValidAsync(PackageArchiveReader packageArchiveReader);
    }
}