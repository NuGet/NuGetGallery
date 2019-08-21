// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class V3SearchResult: SearchResult
    {
        [JsonProperty("@context")]
        public AtContext AtContext { get; set; }

        public DateTime? LastReopen { get; set; }

        public IList<V3SearchResultEntry> Data { get; set; }
    }
}