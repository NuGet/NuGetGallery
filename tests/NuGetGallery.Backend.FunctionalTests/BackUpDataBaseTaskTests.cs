using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;


namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// Test cases for the BackupDataBase task.
    /// </summary>
    [TestClass]
    public class BackUpDataBaseTaskTests
    {
        [TestMethod]
        [Description(" Invokes the BackupDataBase task and checks if the database progresses from copying to online state")]
        [Priority(0)]
        public void BackUpDataBaseTaskTest()
        {        
            string backupName =TaskInvocationHelper.InvokeBackUpDataBaseTask(false);
            Assert.IsTrue(DataBaseHelper.VerifyDataBaseCreation(backupName), " The back up with name {0} didnt get created properly. See logs for details", "backupName");
        }

        [TestMethod]
        [Description("Checks if  BackupDataBase task returns when a backup is already in progress")]
        [Priority(1)]
        public void BackUpDataBaseTaskReturnsIfBackUpInProgressTest()
        {
            string backupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(false);
            Assert.IsNotNull(backupName, " The backup with name {0} didnt get created properly.", backupName);
            Assert.IsTrue(DataBaseHelper.GetDataBaseState(backupName) == DataBaseState.Copying);
            string secondBackupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(false);
            Assert.IsNull(secondBackupName, " BackUp task didnt skip when a backup is already in progress");
            //Verify the completion if first db. So that consecutive tests will not get affected.
            Assert.IsTrue(DataBaseHelper.VerifyDataBaseCreation("Backup_20130412232156"), " The back up with name {0} didnt get created properly. See logs for details", "backupName");
        }

        [TestMethod]
        [Description("Checks if WhatIf is a no ops")]
        [Priority(2)]
        public void BackUpDataBaseTaskWhatIfTest()
        {
            string backupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(true);
            Assert.IsNull(backupName, " The backup with name {0} got created when run in WhatIfMode.", backupName);
        }

        [TestMethod]
        [Description("Checks if BackUpDataBase returns if the source database doesn't satisy the IfOlderThan constraint")]
        [Priority(2)]
        public void BackUpDataBaseTaskIfOlderThanTest()
        {
            string backupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(false);
            Assert.IsTrue(DataBaseHelper.VerifyDataBaseCreation(backupName), " The back up with name {0} didnt get created properly. See logs for details", "backupName");
            //Try to create a second one with if
            string secondbackupName = TaskInvocationHelper.InvokeBackUpDataBaseTask(false, 4 * 60 * 60 * 1000);
        }
    }
}
