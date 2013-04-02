using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Threading;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public abstract class BackupDatabaseBaseJob : WorkerJob
    {
        protected void WaitForCompletion(IBackupDatabase backupDatabaseTask)
        {
            StatusMessage = DatabaseBackupHelper.WaitForCompletion(backupDatabaseTask);
        }
    }
}
