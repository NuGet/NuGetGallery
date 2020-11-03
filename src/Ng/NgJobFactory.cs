// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Ng.Jobs;
using NuGet.Services.Logging;

namespace Ng
{
    public static class NgJobFactory
    {
        public static IDictionary<string, Type> JobMap = new Dictionary<string, Type>()
        {
            { "db2catalog", typeof(Db2CatalogJob) },
            { "db2monitoring", typeof(Db2MonitoringJob) },
            { "catalog2dnx", typeof(Catalog2DnxJob) },
            { "lightning", typeof(LightningJob) },
            { "catalog2monitoring", typeof(Catalog2MonitoringJob) },
            { "monitoring2monitoring", typeof(Monitoring2MonitoringJob) },
            { "monitoringprocessor", typeof(MonitoringProcessorJob) },
            { "catalog2icon", typeof(Catalog2IconJob) },
        };

        public static NgJob GetJob(
            string jobName,
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
        {
            if (JobMap.ContainsKey(jobName))
            {
                return
                    (NgJob)
                    JobMap[jobName].GetConstructor(new[] { typeof(ILoggerFactory), typeof(ITelemetryClient), typeof(IDictionary<string, string>) })
                        .Invoke(new object[] { loggerFactory, telemetryClient, telemetryGlobalDimensions });
            }

            throw new ArgumentException("Missing or invalid job name!");
        }
    }
}
