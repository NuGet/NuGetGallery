using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;
using System.Collections.Generic;

namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// This class tests the "CreateWarehouseArtifactTask".
    /// </summary>
    [TestClass]
    public class CreateWarehouseArtifactsTaskTests : OpsTestBase
    {
        [TestMethod]
        [Description("Invokes the task and checks if the appropriate tables and stored proc are created in the speicfied warehouse DB")]
        [Priority(0)]
        public void CreateWarehouseArtifactTest()
        {
            string warehouseDbName = "Warehouse" + DateTime.Now.Ticks.ToString();
            base.CreateAndVerifyNewWareHouseDb(warehouseDbName);

        }
    }
}
