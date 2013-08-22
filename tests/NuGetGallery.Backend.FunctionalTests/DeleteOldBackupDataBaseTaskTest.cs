using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;
using System.Collections.Generic;


namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// This class tests the "DeleteOldBackupDataBaseTask"
    /// </summary>
    [TestClass]
    public class DeleteOldBackupDataBaseTaskTest
    {

        [TestMethod]
        [Description("Triggers the DeleteBackupDataBaseTask and checks if the expected backups have actually been deleted")]
        public void DeleteOldBackupDataBase()
        {
            int backupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
           
            //Last 5 backups will be preserved by the task. Hence if the current backupcount is less than five create more backup to trigger a delete.
            if (backupCount < 5) 
            { 
             
                int toCreateCount = 6 - backupCount;
                for (int i = 0; i < toCreateCount; i++)
                {
                    string backupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(false);
                    Assert.IsTrue(DataBaseHelper.VerifyDataBaseCreation(backupName), " The back up with name {0} didnt get created properly. See logs for details", "backupName");
                }
            }
            //Check that the expected count of backups (6) is being present now.
            backupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
            Assert.IsTrue((backupCount == 6), " The number of backups are not as expected after creating new backups. Actual : {0}, Expected : {1}", backupCount, 6);

            //Invoke the delete task.
            TaskInvocationHelper.InvokeDeleteOldDatabseBackupsTask();
            int newbackupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
            //Actual count of backups should be 5 after invoking the task.
            Assert.IsTrue((5 == newbackupCount), "Backups not deleted properly through DeleteOldBackupDatabase task.");
        }

        [TestMethod]
        [Description("Triggers the DeleteBackupDataBaseTask and checks if the backups are not deleted if the number of backups are less than 5")]
        public void DeleteOldBackupSkipsIfNumberofBackupsLessThan5()
        {
            int backupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
            //TDB: Update the test after the bug https://github.com/NuGet/NuGetOperations/issues/32 is fixed.
            if (backupCount > 5)
            {   //If count > 5, delete all of the, except 2 backups.
                List<Database> dbs = DataBaseHelper.GetAllDatabaseBackups();
                int toDeleteCount = backupCount-2;
                for (int i = 0; i < toDeleteCount; i++)
                {
                    DataBaseHelper.DeleteDataBase(dbs[i].Name);
                }
            }
            backupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
            //Invoke the task. The count of backups should remain the  same (2) before and after invoking the task.
            TaskInvocationHelper.InvokeDeleteOldDatabseBackupsTask();
            int newbackupCount = DataBaseHelper.GetTotalBackupDataBaseCount();
            Assert.IsTrue((backupCount == newbackupCount), "The count of the backup database is not as expected after executing the DeleteOldDatabaseBackupsTask. Actual : {0}, expected : {1}", newbackupCount, backupCount);
        }
    }
}
