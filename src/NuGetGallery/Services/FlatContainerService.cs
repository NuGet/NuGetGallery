// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;

namespace NuGetGallery
{
    public class FlatContainerService : IFlatContainerService
    {
        private readonly IServiceDiscoveryClient _serviceDiscoveryClient;

        public FlatContainerService(IServiceDiscoveryClient serviceDiscoveryClient)
        {
            _serviceDiscoveryClient = serviceDiscoveryClient ?? throw new ArgumentNullException(nameof(serviceDiscoveryClient));
        }

        public async Task<string> GetLicenseFileFlatContainerUrlAsync(string packageId, string packageVersion)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            var packageBaseAddress = await _serviceDiscoveryClient.GetEndpointsForResourceType(GalleryConstants.PackageBaseAddress);

            var packageBaseUri = new Uri(packageBaseAddress.First().AbsoluteUri.TrimEnd('/') + "/");   // make sure that there is one "/" at the end of the Uri.
            var licenseFileRelativeUri = string.Join("/", new string[] { packageId.ToLowerInvariant(), NuGetVersionFormatter.Normalize(packageVersion).ToLowerInvariant(), CoreConstants.LicenseFileName });

            return new Uri(packageBaseUri, licenseFileRelativeUri).AbsoluteUri;
        }
    }
}