using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace NuGetGallery.Operations
{
    [Command("exportcurrentrankings", "Export current rankings", AltName = "exrank")]
    public class ExportCurrentRankingsTask : DatabaseAndStorageTask
    {
        public override void ExecuteCommand()
        {
            Log.Info("Export current rankings begin");

            CreateRankingsBlob();
        }

        private void CreateRankingsBlob()
        {
            string overallSql = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.Ranking_Overall.sql");
            string projectSql = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.Ranking_Project.sql");

            string connectionString = "Server=tcp:ig2vd9xfaa.database.windows.net;Database=NuGetWarehouse;User ID=nugetwarehouse-sa@ig2vd9xfaa;Password=7999b095-4b09;Trusted_Connection=False;Encrypt=True;Connection Timeout=90;";

            JObject report = new JObject();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(overallSql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                JArray array = new JArray();

                while (reader.Read())
                {
                    string packageId = reader.GetString(0);
                    array.Add(packageId.ToLowerInvariant());
                }

                Log.Info("{0} {1}", "Rank", array.Count);

                report.Add("Rank", array);
            }

            IList<string> projectTypes = GetProjectTypes();

            foreach (string projectGuid in projectTypes)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(projectSql, connection);
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 60 * 5;

                    command.Parameters.AddWithValue("ProjectGuid", projectGuid);
                    SqlDataReader reader = command.ExecuteReader();

                    JArray array = new JArray();

                    while (reader.Read())
                    {
                        string packageId = reader.GetString(0);
                        array.Add(packageId.ToLowerInvariant());
                    }

                    Log.Info("{0} {1}", projectGuid, array.Count);

                    if (array.Count > 0 && projectGuid != "(unknown)")
                    {
                        report.Add(projectGuid, array);
                    }
                }
            }

            Log.Info("creating blob");

            CreateBlob("all.json", report);
        }

        private IList<string> GetProjectTypes()
        {
            string cmdText = "SELECT ProjectTypes FROM Dimension_Project";

            IList<string> result = new List<string>();

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string projectType = reader.GetString(0);

                    result.Add(projectType);
                }
            }

            return result;
        }

        private void CreateBlob(string blobName, JToken data)
        {
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("ranking");

            container.CreateIfNotExists();  // this can throw if the container was just deleted a few seconds ago
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            blockBlob.Properties.ContentType = "application/json";

            MemoryStream stream = new MemoryStream();
            using (TextWriter textWriter = new StreamWriter(stream))
            {
                using (JsonWriter jsonWriter = new JsonTextWriter(textWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;

                    data.WriteTo(jsonWriter);

                    textWriter.Flush();
                    stream.Seek(0, SeekOrigin.Begin);
                    blockBlob.UploadFromStream(stream);
                }
            }
        }
    }
}
