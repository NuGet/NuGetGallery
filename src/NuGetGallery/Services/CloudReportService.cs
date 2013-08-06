using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public class CloudReportService : IReportService
    {
        private string _connectionString;

        public CloudReportService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string> Load(string name)
        {
            //  In NuGet we always use lowercase names for all blobs in Azure Storage
            name = name.ToLowerInvariant();

            string connectionString = _connectionString;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("stats");
            CloudBlockBlob blob = container.GetBlockBlobReference("popularity/" + name);

            MemoryStream stream = new MemoryStream();

            await Task.Factory.FromAsync(blob.BeginDownloadToStream(stream, null, null), blob.EndDownloadToStream);

            stream.Seek(0, SeekOrigin.Begin);

            string content;
            using (TextReader reader = new StreamReader(stream))
            {
                content = reader.ReadToEnd();
            }

            return content;
        }
    }
}