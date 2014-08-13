using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class WarehouseHelper
    {
        static Newtonsoft.Json.Linq.JArray GetNextBatch(string connectionString, ref int lastKey, out DateTime minDownloadTimeStamp, out DateTime maxDownloadTimeStamp)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT TOP(1000) 
	                    PackageStatistics.[Key],
	                    PackageStatistics.[TimeStamp],
	                    ISNULL(PackageStatistics.UserAgent, ''),
	                    ISNULL(PackageStatistics.Operation, ''), 
	                    ISNULL(PackageStatistics.DependentPackage, ''),
	                    ISNULL(PackageStatistics.ProjectGuids, ''),
	                    PackageRegistrations.Id,
	                    Packages.[Version],
	                    ISNULL(Packages.Title, ''),
	                    ISNULL(Packages.[Description], ''),
	                    ISNULL(Packages.IconUrl, '')
                    FROM PackageStatistics
                    INNER JOIN Packages ON PackageStatistics.PackageKey = Packages.[Key]
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE PackageStatistics.[Key] > @key
                    ORDER BY PackageStatistics.[Key]";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("key", lastKey);

                SqlDataReader reader = command.ExecuteReader();

                int count = 0;

                minDownloadTimeStamp = DateTime.MaxValue;
                maxDownloadTimeStamp = DateTime.MinValue;

                JArray batch = new JArray();

                while (reader.Read())
                {
                    count++;

                    int key = reader.GetInt32(0);
                    if (key > lastKey)
                    {
                        lastKey = key;
                    }

                    DateTime timeStamp = reader.GetDateTime(1);
                    if (timeStamp < minDownloadTimeStamp)
                    {
                        minDownloadTimeStamp = timeStamp;
                    }

                    if (timeStamp > maxDownloadTimeStamp)
                    {
                        maxDownloadTimeStamp = timeStamp;
                    }

                    JArray row = new JArray();

                    row.Add(reader.GetInt32(0));
                    row.Add(reader.GetDateTime(1).ToString("O"));
                    row.Add(reader.GetString(2));
                    row.Add(reader.GetString(3));
                    row.Add(reader.GetString(4));
                    row.Add(reader.GetString(5));
                    row.Add(reader.GetString(6));
                    row.Add(reader.GetString(7));
                    row.Add(reader.GetString(8));
                    row.Add(reader.GetString(9));
                    row.Add(reader.GetString(10));

                    batch.Add(row);
                }

                Console.WriteLine("{0} {1}", lastKey, count);

                if (count == 0)
                {
                    minDownloadTimeStamp = DateTime.MinValue;
                    return null;
                }

                return batch;
            }
        }

        public static async Task CreateStatisticsCatalogAsync(Storage storage, string connectionString)
        {
            const int BatchSize = 100;
            int i = 0;

            using (CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), 500))
            {
                int lastKey = 0;
                int iterations = 0;

                while (true)
                {
                    iterations++;

                    DateTime minDownloadTimeStamp;
                    DateTime maxDownloadTimeStamp;

                    JArray batch = GetNextBatch(connectionString, ref lastKey, out minDownloadTimeStamp, out maxDownloadTimeStamp);

                    if (batch == null)
                    {
                        break;
                    }

                    writer.Add(new StatisticsCatalogItem(batch, lastKey.ToString(), minDownloadTimeStamp, maxDownloadTimeStamp));

                    if (++i % BatchSize == 0)
                    {
                        await writer.Commit();
                    }
                }

                await writer.Commit();
            }
        }
    }
}
