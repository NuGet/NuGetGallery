// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Ng.Jobs;
using NuGet.Services.Metadata.Catalog;

namespace Ng
{
    public static class NgJobFactory
    {
        public static IDictionary<string, Type> JobMap = new Dictionary<string, Type>()
        {
            { "db2catalog", typeof(Db2CatalogJob) },
            { "db2monitoring", typeof(Db2MonitoringJob) },
            { "catalog2registration", typeof(Catalog2RegistrationJob) },
            { "catalog2lucene", typeof(Catalog2LuceneJob) },
            { "catalog2dnx", typeof(Catalog2DnxJob) },
            { "copylucene", typeof(CopyLuceneJob) },
            { "checklucene", typeof(CheckLuceneJob) },
            { "clearlucene", typeof(ClearLuceneJob) },
            { "db2lucene", typeof(Db2LuceneJob) },
            { "lightning", typeof(LightningJob) },
            { "catalog2monitoring", typeof(Catalog2MonitoringJob) },
            { "monitoring2monitoring", typeof(Monitoring2MonitoringJob) },
            { "monitoringprocessor", typeof(MonitoringProcessorJob) },
            { "catalog2packagefixup", typeof(Catalog2PackageFixupJob) },
            { "catalog2icon", typeof(Catalog2IconJob) },
        };

        public static NgJob GetJob(string jobName, ITelemetryService telemetryService, ILoggerFactory loggerFactory)
        {
            if (JobMap.ContainsKey(jobName))
            {
                return
                    (NgJob)
                    JobMap[jobName].GetConstructor(new[] { typeof(ITelemetryService), typeof(ILoggerFactory) })
                        .Invoke(new object[] { telemetryService, loggerFactory });
            }

            throw new ArgumentException("Missing or invalid job name!");
        }
    }
}
