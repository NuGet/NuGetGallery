// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public class ODataCacheConfiguration : IODataCacheConfiguration
    {
        public const int DefaultGetByIdAndVersionCacheTimeInSeconds = 60;
        public const int DefaultSearchCacheTimeInSeconds = 45;
        public const int DefaultFindPackagesByIdCountCacheTimeInSeconds = 0;

        public int GetSpecificPackageCacheTimeInSeconds { get; set; } = DefaultGetByIdAndVersionCacheTimeInSeconds;

        public int FindPackagesByIdCacheTimeInSeconds { get; set; } = DefaultGetByIdAndVersionCacheTimeInSeconds;

        public int FindPackagesByIdCountCacheTimeInSeconds { get; set; } = DefaultFindPackagesByIdCountCacheTimeInSeconds;

        public int SearchCacheTimeInSeconds { get; set; } = DefaultSearchCacheTimeInSeconds;
    }
}