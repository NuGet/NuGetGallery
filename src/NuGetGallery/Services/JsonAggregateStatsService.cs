using System.Data;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class JsonAggregateStatsService : IAggregateStatsService
    {
        private readonly string _connectionString;

        public JsonAggregateStatsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<AggregateStats> GetAggregateStats()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("v3-stats0");
            CloudBlockBlob blob = container.GetBlockBlobReference("totals.json");

            //Check if the report blob is present before processing it.
            if (!blob.Exists())
            {
                throw new StatisticsReportNotFoundException();
            }

            string totals = await blob.DownloadTextAsync();
            var json = JsonConvert.DeserializeObject<AggregateStats>(totals);

            return json;
        }
    }
}
