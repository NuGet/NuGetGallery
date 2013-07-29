using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetOperations.FunctionalTests.Helpers
{
    public class EnvironmentSettings
    {

        #region PrivateFields
        private static string dbConnectionString;
        private static string warehouseConnectionString;
        private static string storageConnectionString;
        private static string storageAccountName;     
        
        
        #endregion PrivateFields

        #region Properties


        public static string WarehouseConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(warehouseConnectionString))
                {                   
                    warehouseConnectionString = Environment.GetEnvironmentVariable("WareHouseConnectionString");
                }

                return warehouseConnectionString;
            }

        }

        public static string DataBaseConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(dbConnectionString))
                {                  
                    dbConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
                }

                return dbConnectionString;
            }
        }
     
        #endregion Properties 


    }
}
