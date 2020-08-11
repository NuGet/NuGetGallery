// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class CatalogPageContext
    {
        [JsonProperty("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("nuget")]
        public string NuGet { get; set; }

        [JsonProperty("@items")]
        public ContextTypeDescription Items { get; set; }

        [JsonProperty("parent")]
        public ContextTypeDescription Parent { get; set; }

        [JsonProperty("commitTimeStamp")]
        public ContextTypeDescription CommitTimestamp { get; set; }

        [JsonProperty("nuget:lastCreated")]
        public ContextTypeDescription LastCreated { get; set; }

        [JsonProperty("nuget:lastEdited")]
        public ContextTypeDescription LastEdited { get; set; }

        [JsonProperty("nuget:lastDeleted")]
        public ContextTypeDescription LastDeleted { get; set; }
    }
}
