// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public class PackageRegistrationInfo
    {
        public string Id { get; }
        public IList<PackageInfo> Versions { get; }

        public PackageRegistrationInfo(string id, params PackageInfo[] versions)
        {
            Id = id;
            Versions = versions;
        }

        public PackageInfo GetVersion(string version)
        {
            return Versions.Single(p => p.Version == version);
        }
    }
}
