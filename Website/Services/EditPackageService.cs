using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class EditPackageService
    {
        private IAppConfiguration _configuration;

        public EditPackageService(
            IAppConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void PostEditPackageRequest(EditPackageRequest newMetadata)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                _configuration.AzureStorageConnectionString);

            var queueClient = storageAccount.CreateCloudQueueClient();
            var editsQueue = queueClient.GetQueueReference("EditPackage");
            editsQueue.CreateIfNotExists();
            var json = new JObject(newMetadata).ToString(Newtonsoft.Json.Formatting.Indented);
            var message = new CloudQueueMessage(json);
            editsQueue.AddMessage(message);
        }
    }
}