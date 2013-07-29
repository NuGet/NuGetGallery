using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;


namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// This class tests the "BackupWarehouseTask".
    /// </summary>
    [TestClass]    
    public class BackupWarehouseTaskTest
    {
        [TestMethod]
        [Description("Invokes the BackupWarehouseTask and does back end verification to see if the database has been created")]
        [Priority(0)]
        public void BackupWarehouseTaskBasicTest()
        {
            string backupName = TaskInvocationHelper.InvokeBackupWarehouseTask();
            Assert.IsTrue(DataBaseHelper.VerifyDataBaseCreation(backupName), " The back up with name {0} didnt get created properly. See logs for details", backupName);
        }
    }
}
