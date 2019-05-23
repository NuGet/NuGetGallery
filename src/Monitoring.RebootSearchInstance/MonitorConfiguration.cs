// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Jobs.Monitoring.PackageLag;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class MonitorConfiguration : SearchServiceConfiguration
    {
        public string FeedUrl { get; set; }
        public string Role { get; set; }
        public string RoleInstanceFormat { get; set; }

        /// <summary>
        /// This configuration is in seconds to match the monitor configuration. If the search instance lag is less
        /// than this duration, that instance is considered healthy.
        /// </summary>
        public int HealthyThresholdInSeconds { get; set; }

        /// <summary>
        /// This configuration is in seconds to match the monitor configuration. If the search instance lag is more
        /// than this duration, that instance is considered unhealthy.
        /// </summary>
        public int UnhealthyThresholdInSeconds { get; set; }

        /// <summary>
        /// The time to wait for a restarted instance to become healthy before moving on.
        /// </summary>
        public TimeSpan WaitForHealthyDuration { get; set; }

        /// <summary>
        /// The time to wait before checking a region for unhealthy instances again.
        /// </summary>
        public TimeSpan SleepDuration { get; set; }

        /// <summary>
        /// How long the process should run before ending (allowing the caller to restart the process as desired).
        /// </summary>
        public TimeSpan ProcessLifetime { get; set; }

        /// <summary>
        /// How frequently to poll an instance that was just restarted to check for it's new health status.
        /// </summary>
        public TimeSpan InstancePollFrequency { get; set; }
    }
}
