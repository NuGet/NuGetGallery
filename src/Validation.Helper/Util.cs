// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;

namespace NuGet.Jobs.Validation.Helper
{
    internal static class Util
    {
        public static async Task<NuGetPackage> GetPackage(
            string galleryBaseAddress, 
            NuGetV2Feed feed, 
            string packageId, 
            string packageVersion)
        {
            // We'll try the normalized version first, then fall back to non-normalized
            // one if it fails.
            var url = GetNormalizedPackageUrl(galleryBaseAddress, packageId, packageVersion);
            var package = await GetPackage(feed, url);
            if (package != null)
            {
                return package;
            }
            url = GetPackageUrl(galleryBaseAddress, packageId, packageVersion);
            return await GetPackage(feed, url);
        }

        private static async Task<NuGetPackage> GetPackage(NuGetV2Feed feed, Uri url)
        {
            return (await feed.GetPackagesAsync(url)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the URL of the OData request to the NuGet service that
        /// would provide the package information for the specified package
        /// by its ID and (non-normalized) version.
        /// </summary>
        /// <param name="galleryBaseAddress">
        /// Base address of the NuGet Gallery V2 API (https://www.nuget.org/api/v2).
        /// </param>
        /// <param name="packageId">Package ID.</param>
        /// <param name="packageVersion">Non-normalized package version.</param>
        /// <returns>URL of the OData request providing package information for the requested package.</returns>
        public static Uri GetPackageUrl(string galleryBaseAddress, string packageId, string packageVersion)
        {
            return new Uri($"{galleryBaseAddress}/Packages?" +
                $"$filter=Id eq '{packageId}' and Version eq '{packageVersion}' and true");
        }

        /// <summary>
        /// Returns the URL of the OData request to the NuGet service that
        /// would provide the package information for the specified package
        /// by its ID and normalized version
        /// </summary>
        /// <param name="galleryBaseAddress">
        /// Base address of the NuGet Gallery V2 API (https://www.nuget.org/api/v2).
        /// </param>
        /// <param name="packageId">Package ID.</param>
        /// <param name="normalizedPackageVersion">Normalized package version.</param>
        /// <returns>URL of the OData request providing package information for the requested package.</returns>
        public static Uri GetNormalizedPackageUrl(string galleryBaseAddress, string packageId, string normalizedPackageVersion)
        {
            return new Uri($"{galleryBaseAddress}/Packages?" +
                $"$filter=Id eq '{packageId}' and NormalizedVersion eq '{normalizedPackageVersion}' and true");
        }
    }
}
