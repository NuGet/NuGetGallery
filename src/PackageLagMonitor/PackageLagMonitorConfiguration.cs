// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class PackageLagMonitorConfiguration : SearchServiceConfiguration
    {
        public string ServiceIndexUrl { get; set; }

        public int RetryLimit { get; set; }

        public int WaitBetweenRetrySeconds { get; set; }
    }
}
