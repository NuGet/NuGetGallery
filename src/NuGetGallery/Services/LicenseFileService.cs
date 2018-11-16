// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class LicenseFileService : ILicenseFileService
    {
        private readonly IAppConfiguration _config;
        public LicenseFileService(IAppConfiguration configuration)
        {
            _config = configuration;
        }
        public async Task<string> GetLicenseFileBlobStoragePath(string packageId, string packageVersion)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            var relativePath = String.Join("/", new string[] { packageId.ToLowerInvariant(), NuGetVersionFormatter.Normalize(packageVersion).ToLowerInvariant(), CoreConstants.LicenseFileName});
            var serviceDiscoveryClient = new ServiceDiscoveryClient(_config.ServiceDiscoveryUri);
            var packageBaseAddress = await serviceDiscoveryClient.GetEndpointsForResourceType(CoreConstants.PackageBaseAddress);

            return String.Concat(packageBaseAddress.First().AbsoluteUri, relativePath);
        }
    }
}