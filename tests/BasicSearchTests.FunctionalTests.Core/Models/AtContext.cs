// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class AtContext
    {
        [JsonProperty("@vocab")]
        public string AtVocab { get; set; }

        [JsonProperty("@base")]
        public string AtBase { get; set; }
    }
}