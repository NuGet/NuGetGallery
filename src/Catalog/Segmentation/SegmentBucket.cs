using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    class SegmentBucket
    {
        SortedList<string, Entry> _entries = new SortedList<string, Entry>();

        public SortedList<string, Entry> Entries { get { return _entries; } }

        public int Count { get; set; }

        public Uri SegmentUri { get; set; }
    }
}
