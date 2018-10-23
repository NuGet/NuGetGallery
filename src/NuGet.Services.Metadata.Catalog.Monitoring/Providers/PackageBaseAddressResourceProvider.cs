// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The resource provider that creates instances of <see cref="PackageBaseAddressResource"/>.
    /// </summary>
    public class PackageBaseAddressResourceProvider : ResourceProvider
    {
        private readonly string _packageBaseAddress;

        public PackageBaseAddressResourceProvider(string packageBaseAddress) :
            base(typeof(PackageBaseAddressResource))
        {
            if (string.IsNullOrEmpty(packageBaseAddress))
            {
                throw new ArgumentException("Package base address is required", nameof(packageBaseAddress));
            }

            _packageBaseAddress = packageBaseAddress;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            return Task.FromResult(new Tuple<bool, INuGetResource>(true, new PackageBaseAddressResource(_packageBaseAddress)));
        }
    }
}
