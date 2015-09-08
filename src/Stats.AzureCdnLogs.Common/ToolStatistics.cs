// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public class ToolStatistics
        : ITrackUserAgent, ITrackEdgeServerIpAddress
    {
        public string Path { get; set; }
        public string UserAgent { get; set; }
        public DateTime EdgeServerTimeDelivered { get; set; }
        public string EdgeServerIpAddress { get; set; }
        public string ToolId { get; set; }
        public string ToolVersion { get; set; }
        public string FileName { get; set; }
    }
}