// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class V3SearchResultEntry
    {
        [JsonProperty("@id")]
        public string AtId { get; set; }

        [JsonProperty("@type")]
        public string AtType { get; set; }

        public string Registration { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Id { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Version { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string Title { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string ProjectUrl { get; set; }

        public string[] Tags { get; set; }

        public string[] Authors { get; set; }

        public long TotalDownloads { get; set; }

        public List<V3SearchResultPackageType> PackageTypes { get; set; }

        public PackageVersion[] Versions { get; set; }
    }
}
