// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// This model is used for both the registration page item (found in a registration index) and for a registration
    /// page fetched on its own.
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-page
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-page-object
    /// </summary>
    public class RegistrationPage : ICommitted
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("commitId")]
        public string CommitId { get; set; }

        [JsonProperty("commitTimeStamp")]
        public DateTimeOffset CommitTimestamp { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        /// <summary>
        /// This property can be null when this model is used as an item in <see cref="RegistrationIndex.Items"/> when
        /// the server decided not to inline the leaf items. In this case, the <see cref="Url"/> property can be used 
        /// fetch another <see cref="RegistrationPage"/> instance with the <see cref="Items"/> property filled in.
        /// </summary>
        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("lower")]
        public string Lower { get; set; }

        [JsonProperty("upper")]
        public string Upper { get; set; }

        [JsonProperty("@context")]
        public RegistrationContainerContext Context { get; set; }
    }
}
