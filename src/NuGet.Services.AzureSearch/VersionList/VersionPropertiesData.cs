// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch
{
    public class VersionPropertiesData
    {
        [JsonConstructor]
        public VersionPropertiesData(bool listed, bool semVer2)
        {
            Listed = listed;
            SemVer2 = semVer2;
        }

        public bool Listed { get; }
        public bool SemVer2 { get; }
    }
}
