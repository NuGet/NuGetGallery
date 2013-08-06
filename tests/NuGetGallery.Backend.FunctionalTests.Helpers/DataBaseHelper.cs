using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Model;

namespace NuGetOperations.FunctionalTests.Helpers
{
    public class DataBaseHelper
    {
        #region PublicMethods    
        
        /// <summary>
        /// Given a db name, checks if the DB creation is in progress and waits till the timeout to see if it goes to online state.
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool VerifyDataBaseCreation(string dbName, int timeOut = 10 * 60 * 1000)
        {
            if (GetDataBaseState(dbName) == DataBaseState.Copying)
            {
                while(GetDataBaseState(dbName) == DataBaseState.Copying && timeOut > 0)
                {
                    timeOut -= 30 * 1000;
                }
                return (GetDataBaseState(dbName) == DataBaseState.Online);
            }
            else
            {
                Console.WriteLine(" The backup database state is not in progress right after the execution of the BackupDataBase task");
                return false;
            }
        }

        /// <summary>
        /// Given the dbName, returns its state.
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        public static DataBaseState GetDataBaseState(string dbName)
        {
            var dbs = MasterDataBaseExecutor.Query<OnlineDatabaseBackup>(
                "SELECT name, state FROM sys.databases WHERE name = @dbName",
                new { dbName });                        

            return  GetDataBaseStateFromCode(dbs.FirstOrDefault().State);
        }


        /// <summary>
        /// Returns the count of total backup databases present in the current server.
        /// </summary>
        /// <returns></returns>
        public static int GetTotalBackupDataBaseCount()
        {
            return MasterDataBaseExecutor.Query<OnlineDatabaseBackup>(
                     "SELECT name FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                     new { state = Util.OnlineState }).ToArray().Length;
        }

        /// <summary>
        /// Returns the list of all database backups present in the server.
        /// </summary>
        /// <returns></returns>
        public static List<OnlineDatabaseBackup> GetAllDatabaseBackups()
        {
            return MasterDataBaseExecutor.Query<OnlineDatabaseBackup>(
                     "SELECT name FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                     new { state = Util.OnlineState }).ToList();
        }

        /// <summary>
        /// Deletes the given database.
        /// </summary>
        /// <param name="dbName"></param>
        public static void DeleteDataBase(string dbName)
        {
             MasterDataBaseExecutor.Execute(string.Format("DROP DATABASE {0}", dbName));
        }

        /// <summary>
        /// Creates a new database with the given name.
        /// </summary>
        /// <param name="dbName"></param>
        public static void CreateDataBase(string dbName, bool waitTillCreation=false)
        {
            MasterDataBaseExecutor.Execute(string.Format("Create DATABASE {0}", dbName));
            if (waitTillCreation)
            {
                VerifyDataBaseCreation(dbName);
            }
        }

        /// <summary>
        /// Given a database returns the count of tables present in it.
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        public static int GetTableCount(string dbName)
        {
            SqlExecutor executor = GetDataBaseExecutorFor(dbName);
            return executor.Query<Int32>("SELECT COUNT (*) FROM information_schema.tables").SingleOrDefault();
        }

        /// <summary>
        /// Returns the total download from the Gallery DB.
        /// </summary>
        /// <returns></returns>
        public static int GetTotalDownCount()           
        {
            return DataBaseExecutor.Query<Int32>("SELECT [TotalDownloadCount]  FROM [dbo].[GallerySettings]").SingleOrDefault();
        }

        /// <summary>
        /// Given the database name, returns the connection string for it.
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        public static string GetConnectionStringForDataBase(string dbName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(DBConnectionString) { InitialCatalog = dbName };
            return connectionStringBuilder.ToString();   
        }

        /// <summary>
        /// Given the database name, returns the sqlexecutor for it ( with a open connection).
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        public static SqlExecutor GetDataBaseExecutorFor(string dbName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(DBConnectionString) { InitialCatalog = dbName };
            SqlConnection connection = new SqlConnection( connectionStringBuilder.ToString());
            SqlExecutor executor = new SqlExecutor(connection);
            if (connection.State == ConnectionState.Closed)
            {
                connection.Open();
            }
            return executor;
        }
        
        #endregion PublicMethods

        #region PrivateMembers

        internal static DataBaseState GetDataBaseStateFromCode(byte state)
        {
            if (state == 7) 
                return DataBaseState.Copying;
            else
                return DataBaseState.Online;
        }

        internal static SqlExecutor MasterDataBaseExecutor
        {
            get
            {
                if (mdbExecutor == null)
                {                   
                    mdbSqlConnection = new SqlConnection(Util.GetMasterConnectionString(EnvironmentSettings.DataBaseConnectionString));
                    mdbExecutor = new SqlExecutor(mdbSqlConnection);
                    mdbSqlConnection.Open();
                   
                }
                return mdbExecutor;
            }     
        }
        
         internal static SqlExecutor DataBaseExecutor
        {
            get
            {
                if (dbExecutor == null)
                {
                    dbSqlConnection = new SqlConnection(EnvironmentSettings.DataBaseConnectionString);
                    dbExecutor = new SqlExecutor(dbSqlConnection);
                    dbSqlConnection.Open();
                }
                return dbExecutor;
            }
        }

         internal static string MasterDBConnectionString
         {
             get
             {
                 return Util.GetMasterConnectionString(EnvironmentSettings.DataBaseConnectionString);
             }
         }

         internal static string DBConnectionString
         {
             get
             {
                 return EnvironmentSettings.DataBaseConnectionString;
             }
         }

         internal static string WarehouseDBConnectionString
         {
             get
             {
                 return EnvironmentSettings.WarehouseConnectionString;
             }
         }


        private static SqlExecutor mdbExecutor;
        private static SqlExecutor dbExecutor;
        private static SqlConnection mdbSqlConnection;
        private static SqlConnection dbSqlConnection;

        #endregion PrivateMembers
    }

    public enum DataBaseState
    {
        Copying,
        Online
    }
}
