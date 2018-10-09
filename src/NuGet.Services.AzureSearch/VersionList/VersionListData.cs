// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch
{
    public class VersionListData
    {
        [JsonConstructor]
        public VersionListData(Dictionary<string, VersionPropertiesData> versionProperties)
        {
            VersionProperties = versionProperties ?? throw new ArgumentNullException(nameof(versionProperties));
        }

        /// <summary>
        /// A dictionary of all versions currently available for this package. This includes listed and unlisted
        /// versions. The key is the full version string (can include build metadata).
        /// </summary>
        [JsonConverter(typeof(SemVerOrderedDictionaryJsonConverter))]
        public IReadOnlyDictionary<string, VersionPropertiesData> VersionProperties { get; }
    }
}
