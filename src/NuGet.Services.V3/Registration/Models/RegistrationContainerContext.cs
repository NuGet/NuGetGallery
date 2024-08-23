// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Catalog;

namespace NuGet.Protocol.Registration
{
    public class RegistrationContainerContext
    {
        [JsonProperty("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("catalog")]
        public string Catalog { get; set; }

        [JsonProperty("xsd")]
        public string Xsd { get; set; }

        [JsonProperty("items")]
        public ContextTypeDescription Items { get; set; }

        [JsonProperty("commitTimeStamp")]
        public ContextTypeDescription CommitTimestamp { get; set; }

        [JsonProperty("commitId")]
        public ContextTypeDescription CommitId { get; set; }

        [JsonProperty("count")]
        public ContextTypeDescription Count { get; set; }

        [JsonProperty("parent")]
        public ContextTypeDescription Parent { get; set; }

        [JsonProperty("tags")]
        public ContextTypeDescription Tags { get; set; }

        [JsonProperty("reasons")]
        public ContextTypeDescription Reasons { get; set; }

        [JsonProperty("packageTargetFrameworks")]
        public ContextTypeDescription PackageTargetFrameworks { get; set; }

        [JsonProperty("dependencyGroups")]
        public ContextTypeDescription DependencyGroups { get; set; }

        [JsonProperty("dependencies")]
        public ContextTypeDescription Dependencies { get; set; }

        [JsonProperty("packageContent")]
        public ContextTypeDescription PackageContent { get; set; }

        [JsonProperty("published")]
        public ContextTypeDescription Published { get; set; }

        [JsonProperty("registration")]
        public ContextTypeDescription Registration { get; set; }

        [JsonProperty("vulnerabilities")]
        public ContextTypeDescription Vulnerabilities { get; set; }
    }
}
