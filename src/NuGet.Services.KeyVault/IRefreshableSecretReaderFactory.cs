// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// An interface that allows caching and refreshing the secrets fetched by secret readers.
    /// </summary>
    public interface IRefreshableSecretReaderFactory : ISecretReaderFactory
    {
        /// <summary>
        /// Refresh the values of the secrets that have already been read and cached. Since the cache is shared between
        /// all <see cref="ISecretReader"/> instances creates, this refresh applies to all secret readers created by
        /// this factory.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        Task RefreshAsync(CancellationToken token);
    }
}
