using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class StorageDownloadLookup : DownloadLookup
    {
        CloudBlockBlob _blob;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageDownloadLookup(CloudStorageAccount storageAccount, string containerName, string blobName)
        {
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);
            _blob = container.GetBlockBlobReference(blobName);
        }

        protected override JObject LoadJson()
        {
            if (!_blob.Exists())
            {
                return null;
            }
            string json = _blob.DownloadText();
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}