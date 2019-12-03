// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Catalog;

namespace NuGet.Protocol.Registration
{
    public class RegistrationLeafContext
    {
        [JsonProperty("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("xsd")]
        public string Xsd { get; set; }

        [JsonProperty("catalogEntry")]
        public ContextTypeDescription CatalogEntry { get; set; }

        [JsonProperty("registration")]
        public ContextTypeDescription Registration { get; set; }

        [JsonProperty("packageContent")]
        public ContextTypeDescription PackageContent { get; set; }

        [JsonProperty("published")]
        public ContextTypeDescription Published { get; set; }
    }
}
