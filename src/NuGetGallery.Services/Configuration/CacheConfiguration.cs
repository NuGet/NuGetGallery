// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public class CacheConfiguration : ICacheConfiguration
    {
        public const int DefaultPackageDependentsCacheTimeInSeconds = 3600;

        public int PackageDependentsCacheTimeInSeconds { get; set; } = DefaultPackageDependentsCacheTimeInSeconds;
    }
}
