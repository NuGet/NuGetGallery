// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class Package
    {
        [JsonProperty("@id")]
        public string AtId { get; set; }

        [JsonProperty("@type")]
        public string AtType { get; set; }

        public string Registration { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Domain { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string Title { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string ProjectUrl { get; set; }

        public IEnumerable<string> Tags { get; set; }

        public IEnumerable<string> Authors { get; set; } 

        public int TotalDownloads { get; set; }

        public IEnumerable<PackageVersion> Versions { get; set; }
    }
}