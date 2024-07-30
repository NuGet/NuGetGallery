// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.Services.PackageHash
{
    public interface IPackageHashCalculator
    {
        Task<string> GetPackageHashAsync(
            PackageSource source,
            PackageIdentity package,
            string hashAlgorithmId,
            CancellationToken token);
    }
}