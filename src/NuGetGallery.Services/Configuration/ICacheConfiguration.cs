// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface ICacheConfiguration
    {

        /// <summary>
        /// The cache duration for the dependent packages in the package details page
        /// </summary>
        int PackageDependentsCacheTimeInSeconds { get; }
    }
}
