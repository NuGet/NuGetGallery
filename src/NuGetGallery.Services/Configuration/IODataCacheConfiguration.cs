// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface IODataCacheConfiguration
    {
        /// <summary>
        /// The cache duration for the OData endpoint that returns a single package by ID and version.
        /// </summary>
        int GetSpecificPackageCacheTimeInSeconds { get; }

        /// <summary>
        /// The cache duration for the OData endpoint that returns all versions for a single package ID.
        /// </summary>
        int FindPackagesByIdCacheTimeInSeconds { get; }

        /// <summary>
        /// The cache duration for the OData endpoint that returns the number of versions for a single package ID.
        /// </summary>
        int FindPackagesByIdCountCacheTimeInSeconds { get; }

        /// <summary>
        /// The cache duration for the OData endpoint that search results and the endpoint that returns the number of
        /// search results.
        /// </summary>
        int SearchCacheTimeInSeconds { get; }
    }
}