// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class PackagesFolderPackagePathProvider : IPackagePathProvider
    {
        public string GetPackagePath(string id, string version)
        {
            var idLowerCase = id.ToLowerInvariant();
            var versionLowerCase = NuGetVersionUtility.NormalizeVersion(version).ToLowerInvariant();

            var packageFileName = PackageUtility.GetPackageFileName(idLowerCase, versionLowerCase);

            return $"packages/{packageFileName}";
        }
    }
}