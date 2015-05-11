// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations.Tasks.Monitoring
{
    [Command("listjoblogs", "Lists available job logs", AltName="ljl")]
    public class ListJobLogsTask : DiagnosticsStorageTask
    {
        public override void ExecuteCommand()
        {
            var joblogs = JobLog.LoadJobLogs(StorageAccount);

            // List logs!
            Log.Info("Available Logs: ");
            foreach (var log in joblogs)
            {
                Log.Info("* {0}", log.JobName);
            }
        }
    }
}
