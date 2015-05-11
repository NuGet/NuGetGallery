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
    public class ReplicatePackageStatisticsJob : WorkerJob
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromSeconds(30);
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
            Logger.Trace("Starting replicate package statistics task.");
            ExecuteTask(new ReplicatePackageStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WarehouseConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                WhatIf = Settings.WhatIf,
                CancellationToken = _cts.Token
            });

            Logger.Trace("Finished replicate package statistics task.");
        }
    }
}