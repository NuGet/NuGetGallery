// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Registration
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-leaf-object-in-a-page
    /// </summary>
    public class RegistrationLeafItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("catalogEntry")]
        public RegistrationCatalogEntry CatalogEntry { get; set; }

        [JsonProperty("packageContent")]
        public string PackageContent { get; set; }
    }
}
