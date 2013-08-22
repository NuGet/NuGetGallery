using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;
using System.Collections.Generic;

namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// Tests for ReplicatePackageStatisticsTask
    /// </summary>
    [TestClass]
    public class ReplicatePackageStatisticsTaskTest : OpsTestBase
    {
        [TestMethod]
        [Description(" Download a package to populate the statistisc table and invokes the replicate task to see if the new row gets replicated")]
        public void ReplicatePackageStatisticsTaskBasicTest()
        {
            //Create a new DB and create artifacts in it.
            string warehouseDbName = "Warehouse" + DateTime.Now.Ticks.ToString();
            base.CreateAndVerifyNewWareHouseDb(warehouseDbName);
            //Invoke the replicate task initially.
            int count = TaskInvocationHelper.InvokeReplicatePackageStatisticsTask(DataBaseHelper.GetConnectionStringForDataBase(warehouseDbName));
            //upload a new package and download it.
            string packageId = DateTime.Now.Ticks.ToString();
            PackageHelper.UploadNewPackage(packageId);
            PackageHelper.DownloadPackage(packageId);
            //invoke the task again.
            count = TaskInvocationHelper.InvokeReplicatePackageStatisticsTask(DataBaseHelper.GetConnectionStringForDataBase(warehouseDbName));
            //Check that the count of rows replicated is one now.
            //TDB : This might not be one always ( in case if there is some additional download. The test assumes that no one else is using the BVT environment).
            Assert.IsTrue((count == 1), "The count of packages being replicated after downloading one package is not one. Actual count : {1}", count);
        }
    }
}
