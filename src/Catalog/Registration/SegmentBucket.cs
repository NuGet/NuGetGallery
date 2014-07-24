using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    class SegmentBucket
    {
        SortedList<string, SegmentEntry> _entries = new SortedList<string, SegmentEntry>();

        public SortedList<string, SegmentEntry> Entries { get { return _entries; } }

        public int Count { get; set; }

        public Uri SegmentUri { get; set; }
    }
}
