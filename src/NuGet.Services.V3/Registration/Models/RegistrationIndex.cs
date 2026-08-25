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
        /// <summary>
        /// The name of the root-level property that contains registration-scoped (ID-level) metadata.
        /// </summary>
        public const string MetadataPropertyName = "metadata";

        /// <summary>
        /// The key within <see cref="Metadata"/> under which sponsorship URLs are stored.
        /// </summary>
        public const string SponsorshipUrlsMetadataKey = "sponsorshipUrls";

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

        /// <summary>
        /// A container for registration-scoped (ID-level) metadata.
        /// </summary>
        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; }

        [JsonProperty("items")]
        public List<RegistrationPage> Items { get; set; }

        [JsonProperty("@context")]
        public RegistrationContainerContext Context { get; set; }
    }
}
