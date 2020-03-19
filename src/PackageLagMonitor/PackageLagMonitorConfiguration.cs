// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class PackageLagMonitorConfiguration
    {
        public string ServiceIndexUrl { get; set; }

        public int WaitBetweenRetrySeconds { get; set; }

        public int RetryLimit { get; set; }

        public List<RegionInformation> RegionInformations { get; set; }
    }
}
