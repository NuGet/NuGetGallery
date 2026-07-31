// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    /// <summary>
    /// Determines whether an external icon URL is allowed to be fetched by the
    /// icon ingestion pipeline. Implementations are expected to mitigate
    /// Server-Side Request Forgery (SSRF) by rejecting URLs that target
    /// loopback, private, link-local, or otherwise non-public network
    /// destinations.
    /// </summary>
    public interface IExternalIconUrlPolicy
    {
        Task<bool> IsAllowedAsync(Uri iconUrl, CancellationToken cancellationToken);
    }
}
