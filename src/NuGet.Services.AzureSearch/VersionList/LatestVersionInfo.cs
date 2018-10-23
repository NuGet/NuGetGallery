// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    public class LatestVersionInfo
    {
        public LatestVersionInfo(NuGetVersion parsedVersion, string fullVersion, string[] listedFullVersions)
        {
            ParsedVersion = parsedVersion ?? throw new ArgumentNullException(nameof(parsedVersion));
            FullVersion = fullVersion ?? throw new ArgumentNullException(fullVersion);
            ListedFullVersions = listedFullVersions ?? throw new ArgumentNullException(nameof(listedFullVersions));
        }

        public NuGetVersion ParsedVersion { get; }
        public string FullVersion { get; }
        public string[] ListedFullVersions { get; }
    }
}
