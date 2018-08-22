// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class FlatContainerPackagePathProvider : IPackagePathProvider
    {
        private readonly string _container;

        public FlatContainerPackagePathProvider(string container)
        {
            _container = container;
        }

        public string GetPackagePath(string id, string version)
        {
            var idLowerCase = id.ToLowerInvariant();
            var versionLowerCase = NuGetVersionUtility.NormalizeVersion(version).ToLowerInvariant();
            var packageFileName = PackageUtility.GetPackageFileName(idLowerCase, versionLowerCase);

            return $"{_container}/{idLowerCase}/{versionLowerCase}/{packageFileName}";
        }
    }
}
