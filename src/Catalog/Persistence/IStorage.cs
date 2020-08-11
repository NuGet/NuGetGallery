// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IStorage
    {
        Uri BaseAddress { get; }

        Task CopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellation);
        Task DeleteAsync(Uri resourceUri, CancellationToken cancellationToken, DeleteRequestOptions deleteRequestOptions = null);
        Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(
            Uri resourceUri,
            CancellationToken cancellationToken);
        Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken);
        Task<StorageContent> LoadAsync(Uri resourceUri, CancellationToken cancellationToken);
        Task<string> LoadStringAsync(Uri resourceUri, CancellationToken cancellationToken);
        Uri ResolveUri(string relativeUri);
        Task SaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken);
    }
}