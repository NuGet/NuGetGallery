using System.Globalization;
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

        public void PostEditPackageRequest(EditPackageRequest newMetadata, string callbackAddress, string editId)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                _configuration.AzureStorageConnectionString);

            var queueClient = storageAccount.CreateCloudQueueClient();
            var editsQueue = queueClient.GetQueueReference("EditPackage");
            editsQueue.CreateIfNotExists();
            var json = new JObject(newMetadata);

            json["CallbackAddress"] = callbackAddress;
            json["EditId"] = editId;

            var message = new CloudQueueMessage(json.ToString(Newtonsoft.Json.Formatting.Indented));
            editsQueue.AddMessage(message);
        }
    }
}