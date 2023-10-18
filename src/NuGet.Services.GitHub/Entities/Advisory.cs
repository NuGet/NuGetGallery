// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;

namespace NuGet.Services.GitHub.Entities
{
    public class Advisory
    {
        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; set; }

        [JsonProperty(PropertyName = "severity")]
        public int Severity { get; set; }

        [JsonProperty(PropertyName = "versions")]
        public string Versions { get; set; }
    }
}