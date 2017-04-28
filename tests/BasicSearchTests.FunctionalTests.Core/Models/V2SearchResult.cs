// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class V2SearchResult
    {
        public int? TotalHits { get; set; }

        public DateTime? IndexTimestamp { get; set; }

        public string Index { get; set; }

        public IList<V2SearchResultEntry> Data { get; set; }
    }
}