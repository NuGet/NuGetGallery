// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class NuGetVersionUtility
    {
        public static string NormalizeVersion(string version)
        {
            NuGetVersion parsedVersion;
            if (!NuGetVersion.TryParse(version, out parsedVersion))
            {
                return version;
            }

            return parsedVersion.ToNormalizedString();
        }
        
        public static string NormalizeVersionRange(string versionRange, string defaultValue)
        {
            VersionRange parsedVersionRange;
            if (!VersionRange.TryParse(versionRange, out parsedVersionRange))
            {
                return defaultValue;
            }

            return parsedVersionRange.ToNormalizedString();
        }

        public static string GetFullVersionString(string version)
        {
            NuGetVersion parsedVersion;
            if (!NuGetVersion.TryParse(version, out parsedVersion))
            {
                return version;
            }

            return parsedVersion.ToFullString();
        }
    }
}
