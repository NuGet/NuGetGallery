using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading;

namespace NuGetGallery.Operations
{
    [Command("replicatepackagestatistics", "Replicates any new package statistics", AltName = "repstats", MaxArgs = 0, IsSpecialPurpose = true)]
    public class ReplicatePackageStatisticsTask : DatabaseTask
    {
        [Option("Connection string to the warehouse database server", AltName = "wdb")]
        public SqlConnectionStringBuilder WarehouseConnectionString { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public int Count { get; private set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            if (CurrentEnvironment != null && WarehouseConnectionString == null)
            {
                WarehouseConnectionString = CurrentEnvironment.WarehouseDatabase;
            }
            ArgCheck.RequiredOrConfig(WarehouseConnectionString, "WarehouseConnectionString");
        }

        public override void ExecuteCommand()
        {
            var source = ConnectionString.DataSource;
            var destination = WarehouseConnectionString.DataSource;

            Log.Trace("Connecting to '{0}' to replicate package statistics to '{1}'.", source, destination);

            const int BatchSize = 1000;                 //  number of rows to collect from the source
            const int ExpectedSourceMaxQueryTime = 5;   //  if the query from the source database takes longer than this we must be busy
            const int PauseDuration = 10;               //  pause applied when the queries to the source are taking a long time 

            Count = Replicate(ConnectionString.ConnectionString, WarehouseConnectionString.ConnectionString, BatchSize, ExpectedSourceMaxQueryTime, PauseDuration);
        }

        public static int GetLastOriginalKey(string connectionString)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand("GetLastOriginalKey", connection);
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 120;

                    SqlParameter resultParam = command.CreateParameter();
                    resultParam.Direction = ParameterDirection.Output;
                    resultParam.DbType = DbType.Int32;
                    resultParam.ParameterName = "@OriginalKey";

                    command.Parameters.Add(resultParam);

                    command.ExecuteNonQuery();

                    if (resultParam.Value is DBNull)
                    {
                        return 0;
                    }

                    return (int)resultParam.Value;
                }
            }
            catch (Exception e)
            {
                string msg = string.Format("Exception in GetLastOriginalKey (warehouse side): {0}", e.Message);
                throw new ApplicationException(msg, e);
            }
        }

        private static DownloadBatch GetDownloadRecords(string connectionString, int originalKey, int top)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @";
                        SELECT TOP(@top) 
                          PackageStatistics.[Key] 'OriginalKey', 
                          PackageRegistrations.[Id] 'PackageId', 
                          Packages.[Version] 'PackageVersion', 
                          ISNULL(PackageStatistics.[UserAgent], '') 'DownloadUserAgent', 
                          ISNULL(PackageStatistics.[Operation], '') 'DownloadOperation', 
                          PackageStatistics.[Timestamp] 'DownloadTimestamp' 
                        FROM PackageStatistics 
                        INNER JOIN Packages ON PackageStatistics.PackageKey = Packages.[Key] 
                        INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.PackageRegistrationKey 
                        WHERE PackageStatistics.[Key] > @originalKey 
                    ";

                    SqlCommand command = new SqlCommand(sql, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 120;

                    command.Parameters.Add("@originalKey", SqlDbType.Int);
                    command.Parameters["@originalKey"].Value = originalKey;

                    command.Parameters.Add("@top", SqlDbType.Int);
                    command.Parameters["@top"].Value = top;

                    SqlDataReader reader = command.ExecuteReader();

                    return new DownloadBatch(reader);
                }
            }
            catch (Exception e)
            {
                string msg = string.Format("Exception in GetDownloadRecords (gallery side): {0}", e.Message);
                throw new ApplicationException(msg, e);
            }
        }

        private static void PutDownloadRecords(string connectionString, DownloadBatch batch, CancellationToken ct)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (DownloadBatchRow row in batch.Rows)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        SqlCommand command = new SqlCommand("AddDownloadFact", connection);
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 120;

                        command.Parameters.AddWithValue("@OriginalKey", row.OriginalKey);
                        command.Parameters.AddWithValue("@PackageId", row.PackageId);
                        command.Parameters.AddWithValue("@PackageVersion", row.PackageVersion);
                        command.Parameters.AddWithValue("@DownloadUserAgent", row.DownloadUserAgent);
                        command.Parameters.AddWithValue("@DownloadOperation", row.DownloadOperation);
                        command.Parameters.AddWithValue("@DownloadTimestamp", row.DownloadTimestamp);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                string msg = string.Format("Exception in PutDownloadRecords (warehouse side): {0}", e.Message);
                throw new ApplicationException(msg, e);
            }
        }

        private int Replicate(string source, string destination, int batchSize, int expectedSourceMaxQueryTime, int pauseDuration)
        {
            Log.Trace("Check for data to replicate.");

            int total = 0;

            bool hasWork;

            do
            {
                int originalKey = GetLastOriginalKey(destination);

                DateTime before = DateTime.Now;
                DownloadBatch batch = GetDownloadRecords(source, originalKey, batchSize);
                DateTime after = DateTime.Now;
                TimeSpan duration = after - before;

                Log.Trace(string.Format("source query duration: {0} seconds", duration.TotalSeconds));

                if (batch.Rows.Count > 0)
                {
                    hasWork = true;
                    PutDownloadRecords(destination, batch, CancellationToken);

                    if (CancellationToken.IsCancellationRequested)
                    {
                        return total;
                    }

                    Log.Trace(string.Format("replicated {0} records", batch.Rows.Count));

                    total += batch.Rows.Count;

                    if (duration.TotalSeconds > expectedSourceMaxQueryTime)
                    {
                        Log.Trace("previous source query exceeded threshold so pausing");

                        Thread.Sleep(pauseDuration * 1000);
                    }
                }
                else
                {
                    hasWork = false;
                }

                if (CancellationToken.IsCancellationRequested)
                {
                    return total;
                }
            }
            while (hasWork);

            return total;
        }

        private class DownloadBatchRow
        {
            public DownloadBatchRow(SqlDataReader reader)
            {
                OriginalKey = reader.GetInt32(reader.GetOrdinal("OriginalKey"));
                PackageId = reader.GetString(reader.GetOrdinal("PackageId"));
                PackageVersion = reader.GetString(reader.GetOrdinal("PackageVersion"));
                DownloadUserAgent = reader.GetString(reader.GetOrdinal("DownloadUserAgent"));
                DownloadOperation = reader.GetString(reader.GetOrdinal("DownloadOperation"));
                DownloadTimestamp = reader.GetSqlDateTime(reader.GetOrdinal("DownloadTimestamp"));
            }

            public int OriginalKey { get; private set; }
            public string PackageId { get; private set; }
            public string PackageVersion { get; private set; }
            public string DownloadUserAgent { get; private set; } 
            public string DownloadOperation { get; private set; }
            public SqlDateTime DownloadTimestamp { get; private set; }
        }

        private class DownloadBatch
        {
            List<DownloadBatchRow> rows = new List<DownloadBatchRow>();

            public DownloadBatch(SqlDataReader reader)
            {
                while (reader.Read())
                {
                    rows.Add(new DownloadBatchRow(reader));
                }
            }

            public List<DownloadBatchRow> Rows
            {
                get { return rows; }
            }
        }
    }
}
