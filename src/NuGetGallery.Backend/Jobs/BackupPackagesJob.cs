// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupPackagesJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(10);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(1);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting backup packages task.");
            ExecuteTask(new BackupPackagesTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                BackupStorage = Settings.BackupStorage,
                WhatIf = Settings.WhatIf
            });
            Logger.Info("Finished backup packages task.");
        }
    }
}