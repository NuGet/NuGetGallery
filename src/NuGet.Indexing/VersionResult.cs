// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class VersionResult
    {
        public VersionResult()
        {
            VersionDetails = new List<VersionDetail>();
        }

        public List<VersionDetail> VersionDetails { get; private set; }

        public IEnumerable<string> GetVersions(bool onlyListed)
        {
            return VersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
        }

        public IEnumerable<string> GetStableVersions(bool onlyListed)
        {
            return StableVersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
        }

        public IEnumerable<VersionDetail> StableVersionDetails { get { return VersionDetails.Where(v => v.IsStable); } }
    }
}
