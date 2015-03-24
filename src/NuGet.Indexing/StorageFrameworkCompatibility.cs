using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class StorageFrameworkCompatibility : FrameworkCompatibility
    {
        CloudBlockBlob _blob;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageFrameworkCompatibility(string connectionString)
            : this(CloudStorageAccount.Parse(connectionString))
        {
        }

        public StorageFrameworkCompatibility(CloudStorageAccount storageAccount)
            : this(storageAccount, "ng-search")
        {
        }

        public StorageFrameworkCompatibility(CloudStorageAccount storageAccount, string containerName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName))
        {
        }

        public StorageFrameworkCompatibility(CloudBlobContainer container)
            : this(container.GetBlockBlobReference(@"data/" + FileName))
        {
        }

        public StorageFrameworkCompatibility(CloudBlockBlob blob)
        {
            _blob = blob;
        }

        public StorageFrameworkCompatibility(CloudStorageAccount storageAccount, string containerName, string path)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName).GetBlockBlobReference(path))
        {
        }

        protected override JObject LoadJson()
        {
            string json = _blob.DownloadText();
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}
