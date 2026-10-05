// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// Reports current private artifact usage and the effective limit for a staging owner.
    /// </summary>
    public class StagingQuotaUsage
    {
        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("usedPackages")]
        public int UsedPackages { get; set; }

        [JsonProperty("usedSymbols")]
        public int UsedSymbols { get; set; }

        [JsonProperty("usedArtifacts")]
        public int UsedArtifacts => checked(UsedPackages + UsedSymbols);

        [JsonProperty("limit")]
        public int Limit { get; set; }
    }
}
