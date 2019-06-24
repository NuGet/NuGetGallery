// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class SegmentInfoEventArgs : EventArgs
    {
        public class SegmentInfo
        {
            public string Name { get; set; }
            public int NumDocs { get; set; }
        }

        public SegmentInfoEventArgs(IReadOnlyCollection<SegmentInfo> segments)
        {
            Segments = segments;
        }

        public IReadOnlyCollection<SegmentInfo> Segments { get; set; }
    }
}