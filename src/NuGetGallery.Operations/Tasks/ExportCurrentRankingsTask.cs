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

            IDictionary<string, int> overallRanking = GetOverallRanking();
            CreateRankingBlob("Overall", overallRanking);

            CreatePerProjectRankings();
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

        public IDictionary<string, int> GetRankingForProject(string projectGuid)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();

            string cmdText = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.Ranking_Project.sql");

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                command.Parameters.AddWithValue("ProjectGuid", projectGuid);
                SqlDataReader reader = command.ExecuteReader();

                int order = 1;

                while (reader.Read())
                {
                    string packageId = reader.GetString(0);

                    result[packageId] = order++;
                }
            }

            return result;
        }

        public void CreatePerProjectRankings()
        {
            IList<string> projectTypes = GetProjectTypes();

            Log.Info("Gathering statistics for {0} project types.", projectTypes.Count);

            IList<string> exported = new List<string>();

            foreach (string projectType in projectTypes)
            {
                if (projectType == "(unknown)")
                {
                    continue;
                }

                IDictionary<string, int> ranking = GetRankingForProject(projectType);

                if (ranking.Count > 0)
                {
                    CreateRankingBlob(projectType, ranking);

                    exported.Add(projectType);
                }

                Console.WriteLine("{0}\t{1}", exported.Count, projectType);
            }

            Log.Info("Exported {0} blobs", exported.Count);

            CreateProjectRankingListingBlob(exported);
        }

        public IDictionary<string, int> GetOverallRanking()
        {
            IDictionary<string, int> rank = new Dictionary<string, int>();

            string sql = ResourceHelper.GetBatchFromSqlFile("NuGetGallery.Operations.Scripts.Ranking_Overall.sql");

            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60 * 5;

                SqlDataReader reader = command.ExecuteReader();

                int order = 1;

                while (reader.Read())
                {
                    string packageId = reader.GetString(0);

                    rank.Add(packageId, order++);
                }
            }

            return rank;
        }

        private void CreateProjectRankingListingBlob(IList<string> projectTypes)
        {
            JArray data = new JArray();
            foreach (string projectType in projectTypes)
            {
                if (projectType != "(unknown)")
                {
                    data.Add(projectType);
                }
            }

            CreateBlob("ProjectTypeList", data);
        }

        private void CreateRankingBlob(string blobName, IDictionary<string, int> ranking)
        {
            JArray data = new JArray();
            foreach (KeyValuePair<string, int> item in ranking)
            {
                data.Add(new JObject { { "id", item.Key }, { "rank", item.Value } });
            }

            CreateBlob(blobName, data);
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
