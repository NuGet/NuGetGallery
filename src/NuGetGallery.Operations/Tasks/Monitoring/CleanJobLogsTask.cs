// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations.Tasks.Monitoring
{
    [Command("cleanjoblogs", "Cleans old job logs", AltName="cjl")]
    public class CleanJobLogsTask : DiagnosticsStorageTask
    {
        [Option("The maximum allowed age. Provide a value as expected by TimeSpan.Parse. Default: 07.00:00 (7 days)")]
        public TimeSpan? MaxAge { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (MaxAge == null)
            {
                MaxAge = TimeSpan.FromDays(7);
            }
        }

        public override void ExecuteCommand()
        {
            // Start by fetching the latest log
            var joblogs = JobLog.LoadJobLogs(StorageAccount);

            // Iterate over each log
            foreach (var joblog in joblogs)
            {
                Log.Info("Cleaning {0}", joblog.JobName);
                foreach (var blob in joblog.Blobs.Where(b => (DateTime.UtcNow - b.ArchiveTimestamp) > MaxAge.Value))
                {
                    try
                    {
                        if (!WhatIf)
                        {
                            // Only delete if it matches.
                            blob.Blob.DeleteIfExists(
                                accessCondition: AccessCondition.GenerateIfMatchCondition(blob.Blob.Properties.ETag));
                        }
                        Log.Info("Deleted {0}", blob.Blob.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException("Failed to delete " + blob.Blob.Name, ex);
                    }
                }
            }
        }
    }
}
