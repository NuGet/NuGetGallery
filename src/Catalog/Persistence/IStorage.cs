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

        /// <summary>
        /// Updates the cache control header on the provided resource URI (blob). This method throws an exception if
        /// the resource does not exist. If the Cache-Control is already set to the provided value, no update is made.
        /// </summary>
        /// <param name="resourceUri">The resource URI, this corresponds to a blob name.</param>
        /// <param name="cacheControl">The Cache-Control header to set on the resource.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the Cache-Control changes, false if the Cache-Control already matched the provided value.</returns>
        Task<bool> UpdateCacheControlAsync(Uri resourceUri, string cacheControl, CancellationToken cancellationToken);
    }
}