using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Threading;

namespace NuGetGallery.Operations.Worker.Jobs
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
            ExecuteAggregateStatisticsTask task = new ExecuteAggregateStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf
            };
            task.Execute();

            Logger.Trace("Finished Execute AggregateStatistics Task.");
        }

        public override void OnStop()
        {
        }
    }
}
