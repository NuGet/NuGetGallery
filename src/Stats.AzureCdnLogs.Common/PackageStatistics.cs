// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageStatistics
        : ITrackUserAgent, ITrackEdgeServerIpAddress
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string UserAgent { get; set; }
        public string Operation { get; set; }
        public string DependentPackage { get; set; }
        public DateTime EdgeServerTimeDelivered { get; set; }
        public string EdgeServerIpAddress { get; set; }
    }
}