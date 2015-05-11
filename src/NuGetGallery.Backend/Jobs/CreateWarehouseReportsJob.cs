// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CreateWarehouseReportsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting create warehouse reports task.");
            ExecuteTask(new CreateWarehouseReportsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                StorageAccount = Settings.MainStorage,
                WhatIf = Settings.WhatIf
            });
            Logger.Info("Finished create warehouse reports task.");
        }
    }
}