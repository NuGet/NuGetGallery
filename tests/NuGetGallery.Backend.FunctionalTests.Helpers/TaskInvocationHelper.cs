using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks;

namespace NuGetOperations.FunctionalTests.Helpers
{
    /// <summary>
    /// This class provides the helper functions to invoke the Ops tasks with appropriate parameters.
    /// </summary>
    public class TaskInvocationHelper
    {

        #region PublicMethods
        public static string InvokeBackUpDataBaseTask(bool whatIf, int ifOlderThan = 30 * 60 * 1000)
        {
            var backupTask = new BackupDatabaseTask
            {
                ConnectionString = new SqlConnectionStringBuilder(DataBaseHelper.DBConnectionString),
                WhatIf = whatIf,
                IfOlderThan = ifOlderThan,
            };

            backupTask.Execute();
            return backupTask.BackupName;
        }

        public static void InvokeAggregateStatsTask()
        {
            ExecuteAggregateStatisticsTask task = new ExecuteAggregateStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(DataBaseHelper.DBConnectionString),
                WhatIf = false,
            };
            task.Execute();
        }

        public static void InvokeDeleteOldDatabseBackupsTask()
        {
            DeleteOldDatabaseBackupsTask task = new DeleteOldDatabaseBackupsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(DataBaseHelper.MasterDBConnectionString),
                WhatIf = false
            };
            task.Execute();
        }

        public static void InvokeCreateWarehouseArtifactTask(string connectionString,bool force)
        {
            CreateWarehouseArtifactsTask task = new CreateWarehouseArtifactsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(connectionString),
                Force = false
            };
            task.Execute();
        }

        public static int InvokeReplicatePackageStatisticsTask(string warehouseConnectionString)
        {
            ReplicatePackageStatisticsTask task = new ReplicatePackageStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(DataBaseHelper.DBConnectionString),
                WarehouseConnectionString = new SqlConnectionStringBuilder(warehouseConnectionString)
            };
            task.Execute();
            return task.Count;
        }

        public static string InvokeBackupWarehouseTask(bool whatIf=false)
        {
            BackupWarehouseTask task = new BackupWarehouseTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(DataBaseHelper.WarehouseDBConnectionString),                
                WhatIf = whatIf
            };
            task.Execute();
            return task.BackupName;
        }
        #endregion PublicMethods
    }
}
