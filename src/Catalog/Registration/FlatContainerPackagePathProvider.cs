// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var idLowerCase = id.ToLowerInvariant();
            var versionLowerCase = NuGetVersionUtility.NormalizeVersion(version).ToLowerInvariant();
            var packageFileName = PackageUtility.GetPackageFileName(idLowerCase, versionLowerCase);

            return $"{_container}/{idLowerCase}/{versionLowerCase}/{packageFileName}";
        }

        public string GetIconPath(string id, string version)
        {
            return GetIconPath(id, version, normalize: true);
        }

        public string GetIconPath(string id, string version, bool normalize)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var idLowerCase = id.ToLowerInvariant();

            string versionLowerCase;
            if (normalize)
            {
                versionLowerCase = NuGetVersionUtility.NormalizeVersion(version).ToLowerInvariant();
            }
            else
            {
                versionLowerCase = version.ToLowerInvariant();
            }

            return $"{_container}/{idLowerCase}/{versionLowerCase}/icon";
        }
    }
}
