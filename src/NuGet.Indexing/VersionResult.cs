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
            AllVersionDetails = new List<VersionDetail>();
        }

        public List<VersionDetail> AllVersionDetails { get; private set; }

        public IEnumerable<VersionDetail> SemVer2VersionDetails { get { return AllVersionDetails; } }

        public IEnumerable<VersionDetail> LegacyVersionDetails { get { return AllVersionDetails.Where(v => !v.IsSemVer2); } }

        public IEnumerable<string> GetVersions(bool onlyListed, bool includeSemVer2)
        {
            if (includeSemVer2)
            {
                return SemVer2VersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
            }

            return LegacyVersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
        }

        public IEnumerable<string> GetStableVersions(bool onlyListed, bool includeSemVer2)
        {
            if (includeSemVer2)
            {
                return StableSemVer2VersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
            }

            return StableLegacyVersionDetails.Where(v => !onlyListed || v.IsListed).Select(v => v.Version);
        }

        public IEnumerable<VersionDetail> StableLegacyVersionDetails { get { return AllVersionDetails.Where(v => v.IsStable && !v.IsSemVer2); } }

        public IEnumerable<VersionDetail> StableSemVer2VersionDetails { get { return AllVersionDetails.Where(v => v.IsStable); } }
    }
}
