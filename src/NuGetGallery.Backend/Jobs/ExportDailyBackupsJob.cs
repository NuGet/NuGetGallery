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
    public class ExportDailyBackupsJob : WorkerJob
    {
        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(15);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new ExportDailyBackupsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.BackupStorage,
                SqlDacEndpoint = Settings.SqlDac,
                WhatIf = Settings.WhatIf
            });
        }
    }
}