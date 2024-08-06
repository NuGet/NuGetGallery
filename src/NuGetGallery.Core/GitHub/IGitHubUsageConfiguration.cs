﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface IGitHubUsageConfiguration
    {
        /// <summary>
        /// Returns the GitHub dependants information about a NuGet package.
        /// 
        /// If a packageId has no information, the NuGetPackageGitHubInformation's TotalRepos will be 0
        /// and the Repos list will be empty
        /// 
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when packageId is null</exception>
        /// <param name="packageId">NuGet package id, cannot be null</param>
        /// <returns>NuGetPackageGitHubInformation that contains the information about a NuGet package.</returns>
        NuGetPackageGitHubInformation GetPackageInformation(string packageId);
    }
}
