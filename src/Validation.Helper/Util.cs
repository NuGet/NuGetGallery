// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
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
            var url = GetPackageUrl(galleryBaseAddress, packageId, packageVersion);
            return (await feed.GetPackagesAsync(url)).FirstOrDefault();
        }

        public static Uri GetPackageUrl(string galleryBaseAddress, string packageId, string packageVersion)
        {
            return new Uri($"{galleryBaseAddress}/Packages?" +
                $"$filter=Id eq '{packageId}' and Version eq '{packageVersion}' and true");
        }
    }
}
