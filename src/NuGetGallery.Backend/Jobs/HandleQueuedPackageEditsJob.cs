// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class HandleQueuedPackageEditsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromSeconds(30);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new HandleQueuedPackageEditsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                WhatIf = Settings.WhatIf
            });
        }
    }
} 