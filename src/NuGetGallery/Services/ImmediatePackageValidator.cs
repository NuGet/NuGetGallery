// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// A validation initiator that immediately marks the package as validated. In other words, no asynchronous
    /// validation is performed by this implementation.
    /// </summary>
    public class ImmediatePackageValidator<TPackage> : IPackageValidationInitiator<TPackage> where TPackage: IPackageEntity
    {
        public Task<PackageStatus> StartValidationAsync(TPackage package)
        {
            return Task.FromResult(PackageStatus.Available);
        }
    }
}