// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public interface IIconProcessor
    {
        Task<Uri> CopyEmbeddedIconFromPackage(
            Stream packageStream,
            string iconFilename,
            IStorage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion);
        Task<Uri> CopyIconFromExternalSource(
            Stream iconDataStream,
            IStorage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion);
        Task DeleteIcon(
            Storage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion);
    }
}