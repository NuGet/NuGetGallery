// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Stats.AggregateCdnDownloadsInGallery
{
    [EventSource(Name = "NetFoundation-NuGet-Jobs-AggregateCdnDownloadsInGallery")]
    public class JobEventSource
        : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource()
        {
        }
    }
}