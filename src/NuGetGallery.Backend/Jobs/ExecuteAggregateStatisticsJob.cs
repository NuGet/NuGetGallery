// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Threading;
using NuGetGallery.Operations;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class ExecuteAggregateStatisticsJob : WorkerJob
    {
        ExecuteAggregateStatisticsJob()
        {
        }

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromSeconds(30);
            }
        }

        public override void RunOnce()
        {
            Logger.Trace("Starting Execute AggregateStatistics Task.");
            ExecuteTask(new ExecuteAggregateStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf
            });
            Logger.Trace("Finished Execute AggregateStatistics Task.");
        }
    }
}
