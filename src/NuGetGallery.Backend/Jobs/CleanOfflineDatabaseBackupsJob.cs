// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Tasks.Backups;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CleanOfflineDatabaseBackupsJob : WorkerJob
    {
        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromHours(6);
            }
        }

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(24);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new CleanOfflineDatabaseBackupsTask()
            {
                StorageAccount = Settings.BackupStorage,
                WhatIf = Settings.WhatIf,
            });
        }
    }
}
