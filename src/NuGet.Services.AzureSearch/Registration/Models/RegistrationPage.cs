// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-page
    /// </summary>
    public class RegistrationPage
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }

        [JsonProperty("lower")]
        public string Lower { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("upper")]
        public string Upper { get; set; }
    }
}
