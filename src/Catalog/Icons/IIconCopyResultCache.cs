// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    /// <summary>
    /// Interface for external icon copy results.
    /// </summary>
    public interface IIconCopyResultCache
    {
        /// <summary>
        /// Checks if there is a known result for a certain external icon URL.
        /// </summary>
        /// <param name="iconUrl">External icon URL</param>
        /// <returns>Previous copy result if we have any, null if nothing is associated with specified <paramref name="iconUrl"/>.</returns>
        ExternalIconCopyResult Get(Uri iconUrl);
        
        /// <summary>
        /// Copies the successfully retrieved icon blob from destination storage to the cache and
        /// takes note of success so it could be reused later.
        /// </summary>
        /// <param name="originalIconUrl">The original URL of an icon.</param>
        /// <param name="storageUrl">Storage URL where icon was copied to.</param>
        /// <param name="cacheStorage">Storage to use for icon cache.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Uri of the icon in the cache storage.</returns>
        Task<Uri> SaveExternalIcon(Uri originalIconUrl, Uri storageUrl, IStorage cacheStorage, CancellationToken cancellationToken);

        /// <summary>
        /// Takes note of a failure to copy the external package icon so it's not retried later.
        /// </summary>
        /// <param name="iconUrl">The external icon URL.</param>
        void SaveExternalCopyFailure(Uri iconUrl);


        /// <summary>
        /// Removes the copy result URL (if we have one cached).
        /// </summary>
        /// <param name="externalIconUrl">External icon URL.</param>
        void Clear(Uri externalIconUrl);
    }
}