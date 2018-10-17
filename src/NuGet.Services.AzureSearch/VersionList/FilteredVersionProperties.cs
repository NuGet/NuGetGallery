// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Version properties needed by <see cref="FilteredVersionList"/>. This is a subset of information needed by
    /// <see cref="VersionLists"/>. The extra information not contained in this class (such as
    /// <see cref="VersionPropertiesData.SemVer2"/>) is only used for filtering.
    /// </summary>
    internal class FilteredVersionProperties
    {
        public FilteredVersionProperties(string fullVersion, NuGetVersion parsedVersion, bool listed)
        {
            FullVersion = fullVersion ?? throw new ArgumentNullException(nameof(fullVersion));
            ParsedVersion = parsedVersion ?? throw new ArgumentNullException(nameof(parsedVersion));
            Listed = listed;
        }

        public FilteredVersionProperties(string fullVersion, bool listed)
        {
            if (fullVersion == null)
            {
                throw new ArgumentNullException(nameof(fullVersion));
            }

            ParsedVersion = NuGetVersion.Parse(fullVersion);
            FullVersion = ParsedVersion.ToFullString();
            Listed = listed;
        }

        public string FullVersion { get; }
        public NuGetVersion ParsedVersion { get; }
        public bool Listed { get; }
    }
}
