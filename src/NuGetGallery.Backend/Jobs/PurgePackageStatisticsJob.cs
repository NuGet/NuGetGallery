// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class PurgePackageStatisticsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(30);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            Logger.Trace("Starting purge package statistics task.");
            ExecuteTask(new PurgePackageStatisticsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WarehouseConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                WhatIf = Settings.WhatIf
            });
            Logger.Trace("Finished purge package statistics task.");
        }
    }
}