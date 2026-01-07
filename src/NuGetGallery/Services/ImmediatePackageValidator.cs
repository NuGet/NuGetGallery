// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// A validation initiator that immediately marks the package as validated. In other words, no asynchronous
    /// validation is performed by this implementation.
    /// </summary>
    public class ImmediatePackageValidator<TPackageEntity> : IPackageValidationInitiator<TPackageEntity> 
        where TPackageEntity: IPackageEntity
    {
        public PackageStatus GetPackageStatus(TPackageEntity package)
            => PackageStatus.Available;

        public Task<PackageStatus> StartValidationAsync(TPackageEntity package)
        {
            return Task.FromResult(PackageStatus.Available);
        }
    }
}