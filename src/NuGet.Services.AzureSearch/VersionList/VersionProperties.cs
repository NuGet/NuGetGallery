// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// All properties related to a specific version. This type exists to avoid parsing the <see cref="FullVersion"/>
    /// over and over to get the <see cref="ParsedVersion"/>.
    /// </summary>
    internal class VersionProperties
    {
        public VersionProperties(string fullVersion, NuGetVersion parsedVersion, VersionPropertiesData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Filtered = new FilteredVersionProperties(fullVersion, parsedVersion, Data.Listed);
        }

        public VersionProperties(string fullOrOriginalVersion, VersionPropertiesData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Filtered = new FilteredVersionProperties(fullOrOriginalVersion, Data.Listed);
        }

        public string FullVersion => Filtered.FullVersion;
        public NuGetVersion ParsedVersion => Filtered.ParsedVersion;

        public VersionPropertiesData Data { get; }
        public FilteredVersionProperties Filtered { get; }
    }
}
