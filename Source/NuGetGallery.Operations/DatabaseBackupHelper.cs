using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Operations
{
    public static class DatabaseBackupHelper
    {
        public static string WaitForCompletion(IBackupDatabase backupDatabaseTask)
        {
            if (backupDatabaseTask.SkippingBackup)
            {
                return "backup creation was skipped";
            }
            else
            {
                if (PollStatus(backupDatabaseTask.ConnectionString.ConnectionString, backupDatabaseTask.BackupName))
                {
                    return string.Format("created backup '{0}'", backupDatabaseTask.BackupName);
                }
                else
                {
                    throw new Exception("creating backup failed");
                }
            }
        }

        private static bool PollStatus(string connectionString, string backupName)
        {
            //  poll the state of the database for 30 minutes
            for (int i = 0; i < 60; i++)
            {
                CheckDatabaseStatusTask checkDatabaseStatusTask = new CheckDatabaseStatusTask
                {
                    ConnectionString = new SqlConnectionStringBuilder(connectionString),
                    BackupName = backupName,
                    WhatIf = false // WhatIf isn't used by this task.
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
