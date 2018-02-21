// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;

namespace NuGet.Services.PackageHash
{
    public class PackageHash
    {
        public PackageHash(PackageIdentity identity, string expectedHash)
        {
            Identity = identity;
            ExpectedHash = expectedHash;
        }

        public PackageIdentity Identity { get; }

        /// <summary>
        /// The expected, base64-encoded hash digest.
        /// </summary>
        public string ExpectedHash { get; }
    }
}
