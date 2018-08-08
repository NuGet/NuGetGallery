// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// Initiates validation for a specific package.
    /// </summary>
    public interface IPackageValidationInitiator<TPackageEntity> 
        where TPackageEntity: IPackageEntity
    {
        /// <summary>
        /// Starts the validation for the specified IPackageEntity. The validation can be done asynchronously with respect
        /// to the gallery and therefore may not be complete when the returned <see cref="Task"/> completes. This
        /// pending validation state is indicated by the package having the <see cref="PackageStatus.Validating"/>
        /// status.
        /// </summary>
        /// <param name="package">The <see cref="TPackageEntity"/> to initiate validation for.</param>
        /// <returns>
        /// The task which signals the completion of the validation initiation. The result of the <see cref="Task"/>
        /// is the <see cref="PackageStatus"/> that should be applied to the package.</returns>
        Task<PackageStatus> StartValidationAsync(TPackageEntity package);
    }
}