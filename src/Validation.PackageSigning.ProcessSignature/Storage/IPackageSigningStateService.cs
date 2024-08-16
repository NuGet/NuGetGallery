// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public interface IPackageSigningStateService
    {
        Task SetPackageSigningState(
            int packageKey,
            string packageId,
            string packageVersion,
            PackageSigningStatus status);

        Task<bool> HasPackageSigningStateAsync(int packageKey);
    }
}
