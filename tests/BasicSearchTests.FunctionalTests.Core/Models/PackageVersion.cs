// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class PackageVersion
    {
        public string Version;

        public long Downloads;

        [JsonProperty("@id")]
        public string AtId;
    }
}