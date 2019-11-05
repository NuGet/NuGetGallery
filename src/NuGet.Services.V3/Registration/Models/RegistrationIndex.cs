// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-index
    /// </summary>
    public class RegistrationIndex : ICommitted
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public List<string> Types { get; set; }

        [JsonProperty("commitId")]
        public string CommitId { get; set; }

        [JsonProperty("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("items")]
        public List<RegistrationPage> Items { get; set; }

        [JsonProperty("@context")]
        public RegistrationContainerContext Context { get; set; }
    }
}
