// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class AuxiliaryFilesUpdateTime
    {
        [JsonProperty("owners.json")]
        public DateTime? Owners { get; set; }

        [JsonProperty("downloads.v1.json")]
        public DateTime? Downloads { get; set; }

        [JsonProperty("curatedfeeds.json")]
        public DateTime? CuratedFeeds { get; set; }

        [JsonProperty("rankings.v1.json")]
        public DateTime? Rankings { get; set; }

        [JsonProperty("SearchSettings.v1.json")]
        public DateTime? SearchSettings { get; set; }
    }
}