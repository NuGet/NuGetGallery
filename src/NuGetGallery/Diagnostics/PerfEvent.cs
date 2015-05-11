// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Diagnostics
{
    public class PerfEvent
    {
        public string Source { get; private set; }
        public DateTime TimestampUtc { get; private set; }
        public IEnumerable<KeyValuePair<string, object>> Fields { get; private set; }
        public TimeSpan Duration { get; private set; }

        public PerfEvent(string source, DateTime timestampUtc, TimeSpan duration, IEnumerable<KeyValuePair<string, object>> fields)
        {
            Source = source;
            TimestampUtc = timestampUtc;
            Duration = duration;
            Fields = fields;
        }
    }
}
