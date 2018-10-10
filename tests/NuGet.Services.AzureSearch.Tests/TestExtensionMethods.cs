// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    internal static class TestExtensionMethods
    {
        public static IReadOnlyDictionary<SearchFilters, LatestIndexChanges> Upsert(
            this VersionLists versionList,
            VersionProperties version)
        {
            return versionList.Upsert(version.FullVersion, version.Data);
        }

        public static IReadOnlyDictionary<SearchFilters, LatestIndexChanges> Delete(
            this VersionLists versionList,
            VersionProperties version)
        {
            return versionList.Delete(version.FullVersion);
        }
    }
}
