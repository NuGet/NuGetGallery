// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CleanOnlineDatabaseBackupsJob : WorkerJob
    {
        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(1);
            }
        }

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new CleanOnlineDatabaseBackupsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                BackupStorage = Settings.BackupStorage,
                WhatIf = Settings.WhatIf,
            });
        }
    }
}
