// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupDatabaseJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new BackupDatabaseTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf,
                IfOlderThan = 30,
            });
        }
    }
}