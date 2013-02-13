using System;
using System.ComponentModel.Composition;
using System.Threading;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public abstract class BackupDatabaseBaseJob : WorkerJob
    {
        protected void WaitForCompletion(IBackupDatabase backupDatabaseTask)
        {
            if (backupDatabaseTask.SkippingBackup)
            {
                StatusMessage = "backup creation was skipped";
            }
            else
            {
                if (PollStatus(backupDatabaseTask.ConnectionString, backupDatabaseTask.BackupName))
                {
                    StatusMessage = string.Format("created backup '{0}'", backupDatabaseTask.BackupName);
                }
                else
                {
                    throw new Exception("creating backup failed");
                }
            }
        }

        private bool PollStatus(string connectionString, string backupName)
        {
            //  poll the state of the database for 30 minutes
            for (int i = 0; i < 60; i++)
            {
                CheckDatabaseStatusTask checkDatabaseStatusTask = new CheckDatabaseStatusTask
                {
                    ConnectionString = connectionString,
                    BackupName = backupName,
                    WhatIf = Settings.WhatIf
                };

                checkDatabaseStatusTask.Execute();

                if (checkDatabaseStatusTask.State == 0)
                {
                    return true;
                }

                Thread.Sleep(30 * 1000);
            }

            return false;
        }
    }
}
