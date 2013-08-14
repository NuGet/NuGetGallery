using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;
using System.Collections.Generic;

namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// Base class for all Ops tests.
    /// </summary>
    [TestClass]
    public class OpsTestBase
    {
        #region BaseMethods

        public void CreateAndVerifyNewWareHouseDb(string warehouseDbName)
        {          
            //Create a new database.
            DataBaseHelper.CreateDataBase(warehouseDbName);
            //The initial table count would be 0.
            int count = DataBaseHelper.GetTableCount(warehouseDbName);
            Assert.IsTrue((count == 0), "Initial count of tables is not 0 right after creating warehouse DB. Actual : {0}", count);
            //Invoke the task.
            TaskInvocationHelper.InvokeCreateWarehouseArtifactTask(DataBaseHelper.GetConnectionStringForDataBase(warehouseDbName), false);
            //The task should be have created 8 tables in the database.
            count = DataBaseHelper.GetTableCount(warehouseDbName);
            Assert.IsTrue((count == 8), "Count of tables is not 8 in the warehouse DB after executing the CreateWarehouseArtifactTask. Actual : {0}", count);
            //To do : Add more validations around the data that has to be present in the tables.
        }

        #endregion BaseMethods
    }
}
