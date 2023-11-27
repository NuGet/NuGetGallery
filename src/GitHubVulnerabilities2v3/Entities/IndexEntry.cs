// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;

namespace GitHubVulnerabilities2v3.Entities
{
    public class IndexEntry
    {
        [JsonProperty(PropertyName = "@name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "@id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "@updated")]
        public DateTime Updated { get; set; }

        [JsonProperty(PropertyName = "comment")]
        public string Comment { get; set; }
    }
}