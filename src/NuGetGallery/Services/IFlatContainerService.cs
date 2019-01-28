// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// This interface is used to get the license file path.
    /// </summary>
    public interface IFlatContainerService
    {
        /// <summary>
        /// The function is used to get the license file path.
        /// </summary>
        /// <param name="packageId"> The package ID</param>
        /// <param name="packageVersion"> The package version</param>
        Task<string> GetLicenseFileFlatContainerUrlAsync(string packageId, string packageVersion);
    }
}
