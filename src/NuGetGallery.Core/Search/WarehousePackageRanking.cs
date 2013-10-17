using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    public class WarehousePackageRanking : PackageRanking
    {
        string _storageConnectionString;

        public WarehousePackageRanking(string storageConnectionString)
        {
            _storageConnectionString = storageConnectionString;
        }

        public override IDictionary<string, IDictionary<string, int>> GetProjectRankings()
        {
            IList<string> projectGuids = GetProjectGuids(_storageConnectionString);

            Console.WriteLine("Gathering statistics for project types:");

            IDictionary<string, IDictionary<string, int>> result = new Dictionary<string, IDictionary<string, int>>(); 

            foreach (string projectGuid in projectGuids)
            {
                IDictionary<string, int> ranking = GetRanking(_storageConnectionString, projectGuid);

                if (ranking.Count > 0)
                {
                    result.Add(projectGuid, ranking);
                }

                Console.WriteLine(projectGuid);
            }

            return result;
        }

        public override IDictionary<string, int> GetOverallRanking()
        {
            return GetRanking(_storageConnectionString, "Overall");
        }

        private static IDictionary<string, int> GetRanking(string storageConnectionString, string blobName)
        {
            IDictionary<string, int> ranking = new Dictionary<string, int>();

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("ranking");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

            MemoryStream stream = new MemoryStream();

            blockBlob.DownloadToStream(stream);

            stream.Seek(0, SeekOrigin.Begin);

            using (TextReader textReader = new StreamReader(stream))
            {
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    JArray array = JArray.Load(jsonReader);

                    foreach (JObject item in array)
                    {
                        ranking.Add(item["id"].ToString(), item["rank"].ToObject<int>());
                    }
                }
            }

            return ranking;
        }

        private static IList<string> GetProjectGuids(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("ranking");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("ProjectTypeList");

            IList<string> result = new List<string>();

            MemoryStream stream = new MemoryStream();

            blockBlob.DownloadToStream(stream);

            stream.Seek(0, SeekOrigin.Begin);

            using (TextReader textReader = new StreamReader(stream))
            {
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    JArray array = JArray.Load(jsonReader);

                    foreach (JToken item in array)
                    {
                        result.Add(item.ToString());
                    }
                }
            }

            return result;
        }
    }
}
