// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Gets the base URL for the Package Content resource. For more information,
    /// see: https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource
    /// </summary>
    public class PackageBaseAddressResource : INuGetResource
    {
        public PackageBaseAddressResource(string packageBaseAddress)
        {
            if (string.IsNullOrEmpty(packageBaseAddress))
            {
                throw new ArgumentException("Package base address is required", nameof(packageBaseAddress));
            }

            PackageBaseAddress = packageBaseAddress.TrimEnd('/');
        }

        /// <summary>
        /// The base URL for the Package Content resource.
        /// </summary>
        public string PackageBaseAddress { get; }
    }
}
