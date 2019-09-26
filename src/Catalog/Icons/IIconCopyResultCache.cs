// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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
        /// Stores the copy result for a given external icon URL.
        /// </summary>
        /// <param name="iconUrl">External icon URL.</param>
        /// <param name="newItem">Copy attempt result.</param>
        void Set(Uri iconUrl, ExternalIconCopyResult newItem);

        /// <summary>
        /// Removes the copy result URL (if we have one cached) if destination URL is the expected one. Useful when the package is deleted to prevent subsequent failures.
        /// </summary>
        /// <param name="externalIconUrl">External icon URL.</param>
        /// <param name="targetStorageUrl">Expected storage URL for the icon. Item will only be removed if the cached result is successful copy and target URL is the same as supplied.</param>
        void Clear(Uri externalIconUrl, Uri targetStorageUrl);
    }
}