
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class StorageCategorizationPermission : ICategorizationPermission
    {
        CloudStorageAccount _account;

        public StorageCategorizationPermission(CloudStorageAccount account)
        {
            _account = account;
        }

        public async Task<bool> IsAllowedToSpecifyCategory(string id)
        {
            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("apiapps-v3-index");
            CloudBlockBlob blob = container.GetBlockBlobReference("trusted-packages.json");

            try
            {
                string json = await blob.DownloadTextAsync();

                JObject obj = JObject.Parse(json);

                foreach (string registration in obj["registrations"])
                {
                    if (registration.Equals(id, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}