// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class SemVerHelpers
    {
        public static readonly string SemVerLevelKeySemVer2 = "2";
        public static readonly NuGetVersion SemVer1Level = new NuGetVersion("1.0.0");
        public static readonly NuGetVersion SemVer2Level = new NuGetVersion("2.0.0");

        public static bool ShouldIncludeSemVer2Results(NuGetVersion semVerLevel)
        {
            if (semVerLevel == null)
            {
                return false;
            }

            return semVerLevel >= SemVer2Level;
        }
    }
}
