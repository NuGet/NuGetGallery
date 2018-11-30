// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public interface ICollector
    {
        Task<bool> RunAsync(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken);
    }
}
