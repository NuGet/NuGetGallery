// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class PackagesFolderPackagePathProvider : IPackagePathProvider
    {
        public string GetPackagePath(string id, string version)
        {
            return $"packages/{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg";
        }
    }
}