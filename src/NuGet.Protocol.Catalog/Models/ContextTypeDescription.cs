// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class ContextTypeDescription
    {
        [JsonProperty("@id")]
        public string Id { get; set; }

        [JsonProperty("@container")]
        public string Container { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }
    }
}
