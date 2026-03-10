// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog
{
    public class CursorValueWithUpdates
    {
        public readonly static JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            DateFormatString = "O"
        };

        [JsonProperty("value")]
        public string Value { get; set; }

        // This is for the cursor reader to determine which update (in the list of updates) of the cursor value to read.
        // The timestamp of the update to read should be at least before the current timestamp minus this interval.
        [JsonProperty("minIntervalBeforeToReadUpdate")]
        public TimeSpan MinIntervalBeforeToReadUpdate { get; set; }

        [JsonProperty("updates")]
        public IList<CursorValueUpdate> Updates { get; set; } = new List<CursorValueUpdate>();
    }

    public class CursorValueUpdate
    {
        public CursorValueUpdate(DateTime timeStamp, string value)
        {
            TimeStamp = timeStamp;
            Value = value;
        }

        [JsonProperty("timeStamp")]
        public DateTime TimeStamp { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}
