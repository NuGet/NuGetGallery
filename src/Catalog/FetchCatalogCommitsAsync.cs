// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    internal delegate Task<IEnumerable<CatalogCommit>> FetchCatalogCommitsAsync(
        CollectorHttpClient client,
        ReadCursor front,
        ReadCursor back,
        CancellationToken cancellationToken);
}