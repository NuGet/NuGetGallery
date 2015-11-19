// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class PackageVersion
    {
        public string Version { get; set; }

        public int Downloads { get; set; }

        [JsonProperty("@id")]
        public string AtId { get; set; }
    }
}