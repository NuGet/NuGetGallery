using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Segmentation
{
    class SegmentBucket
    {
        SortedList<string, Entry> _entries = new SortedList<string, Entry>();

        public SortedList<string, Entry> Entries { get { return _entries; } }

        public int Count { get; set; }

        public Uri SegmentUri { get; set; }
    }
}
