// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Aggregates information about index and leaf metadata.
    /// </summary>
    public interface IPackageRegistrationMetadataResource : INuGetResource
    {
        Task<PackageRegistrationIndexMetadata> GetIndexAsync(PackageIdentity package, ILogger log, CancellationToken token);

        Task<PackageRegistrationLeafMetadata> GetLeafAsync(PackageIdentity package, ILogger log, CancellationToken token);
    }
}
