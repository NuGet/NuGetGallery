using NuGetGallery.Operations.Common;
using System;
using System.Data;
using System.Data.SqlClient;

namespace NuGetGallery.Operations
{
    [Command("purgepackagestatisticstask", "Purge Package Statistics", AltName = "purgestats", MaxArgs = 0)]
    public class PurgePackageStatisticsTask : DatabaseTask
    {
        [Option("Connection string to the warehouse database server", AltName = "wdb")]
        public string WarehouseConnectionString { get; set; }

        public PurgePackageStatisticsTask() 
        {
            // Load defaults from environment
            WarehouseConnectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.RequiredOrEnv(WarehouseConnectionString, "WarehouseConnectionString", "NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
        }

        public override void ExecuteCommand()
        {
            var source = Util.GetDbServer(ConnectionString);
            var destination = Util.GetDbServer(WarehouseConnectionString);

            Log.Trace("Connecting to '{0}' to replicate package statistics to '{1}'.", source, destination);

            Purge(ConnectionString, WarehouseConnectionString);
        }

        private void Purge(string source, string destination)
        {
            Log.Trace("Purge data from the production database.");

            int originalKey = ReplicatePackageStatisticsTask.GetLastOriginalKey(destination);

            Log.Info(string.Format("Purging PackageStatistics records that are not in the warehouse"));

            DeletePackageStatistics(source, originalKey);
        }

        private static void DeletePackageStatistics(string connectionString, int warehouseHighWatermark)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                int iterations = 0;
                int rows;
                do
                {
                    string sql = @"
                      DELETE TOP(50000) [PackageStatistics]
                      WHERE [Key] <= @OriginalKey
                      AND [Key] <= (SELECT DownloadStatsLastAggregatedId FROM GallerySettings)
                      AND [TimeStamp] < DATEADD(day, -7, GETDATE())
                    ";

                    SqlCommand command = new SqlCommand(sql, connection);
                    command.CommandType = CommandType.Text;

                    SqlParameter parameter = command.CreateParameter();
                    parameter.DbType = DbType.Int32;
                    parameter.ParameterName = "@OriginalKey";
                    parameter.Value = warehouseHighWatermark;

                    command.Parameters.Add(parameter);
                    rows = command.ExecuteNonQuery();
                }
                while (rows > 0 && iterations++ < 10);
            }
        }
    }
}
