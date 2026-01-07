// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.PackageHash
{
    public class InvalidPackageHash
    {
        public InvalidPackageHash(PackageSource source, PackageHash package, string invalidHash)
        {
            Source = source;
            Package = package;
            InvalidHash = invalidHash;
        }

        public PackageSource Source { get; }
        public PackageHash Package { get; }
        public string InvalidHash { get; }
    }
}
