// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The OData endpoints that support caching. This is used by <see cref="ODataCacheOutputAttribute"/> to specify
    /// which property on <see cref="Services.ODataCacheConfiguration"/> should be used to define the cache time.
    /// </summary>
    public enum ODataCachedEndpoint
    {
        GetSpecificPackage,
        FindPackagesById,
        FindPackagesByIdCount,
        Search,
    }
}