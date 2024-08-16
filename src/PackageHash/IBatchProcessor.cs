// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.PackageHash
{
    public interface IBatchProcessor
    {
        Task<IReadOnlyList<InvalidPackageHash>> ProcessBatchAsync(
            IReadOnlyList<PackageHash> batch,
            string hashAlgorithm,
            CancellationToken token);
    }
}