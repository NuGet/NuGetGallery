// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    internal static class TestExtensionMethods
    {
        public static MutableIndexChanges Upsert(this VersionLists versionList, VersionProperties version)
        {
            return versionList.Upsert(version.FullVersion, version.Data);
        }

        public static MutableIndexChanges Delete(this VersionLists versionList, string fullOrOriginalVersion)
        {
            return versionList.Delete(NuGetVersion.Parse(fullOrOriginalVersion));
        }
    }
}
