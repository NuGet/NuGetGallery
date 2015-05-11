// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks.Monitoring;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CleanJobLogsJob : WorkerJob
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
            ExecuteTask(new CleanJobLogsTask
            {
                StorageAccount = Settings.DiagnosticsStorage,
                WhatIf = Settings.WhatIf,
                MaxAge = TimeSpan.FromDays(7)
            });
        }
    }
}