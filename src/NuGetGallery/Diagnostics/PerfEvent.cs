using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Diagnostics
{
    public class PerfEvent
    {
        public DateTime TimestampUtc { get; private set; }
        public IEnumerable<KeyValuePair<string, object>> Fields { get; private set; }
        public TimeSpan Duration { get; private set; }

        public PerfEvent(DateTime timestampUtc, TimeSpan duration, IEnumerable<KeyValuePair<string, object>> fields)
        {
            TimestampUtc = timestampUtc;
            Duration = duration;
            Fields = fields;
        }
    }
}
