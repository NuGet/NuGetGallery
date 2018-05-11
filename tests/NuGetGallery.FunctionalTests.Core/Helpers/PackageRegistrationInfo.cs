// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public class PackageRegistrationInfo
    {
        public string Id { get; }
        public IList<PackageVersionInfo> Versions { get; }

        public PackageRegistrationInfo(string id, params PackageVersionInfo[] versions)
        {
            Id = id;
            Versions = versions.ToList();
        }

        public PackageVersionInfo GetVersion(string version)
        {
            return Versions.Single(p => p.Version == version);
        }
    }
}
