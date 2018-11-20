﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class LicenseFileBlobStorageService : ILicenseFileBlobStorageService
    {
        private readonly ServiceDiscoveryClient _serviceDiscoveryClient;

        public LicenseFileBlobStorageService(IAppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            _serviceDiscoveryClient = new ServiceDiscoveryClient(configuration.ServiceDiscoveryUri);
        }

        public async Task<string> GetLicenseFileBlobStoragePathAsync(string packageId, string packageVersion)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            var relativePath = string.Join("/", new string[] { packageId.ToLowerInvariant(), NuGetVersionFormatter.Normalize(packageVersion).ToLowerInvariant(), CoreConstants.LicenseFileName});
            var packageBaseAddress = await _serviceDiscoveryClient.GetEndpointsForResourceType(CoreConstants.PackageBaseAddress);

            return string.Concat(packageBaseAddress.First().AbsoluteUri, "/", relativePath);
        }
    }
}