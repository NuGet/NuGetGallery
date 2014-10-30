using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class InitStorageJob : CantonJob
    {
        // this should only run once
        private static bool _complete = false;

        public InitStorageJob(Config config)
            : base(config, null)
        {

        }

        public override async Task RunCore()
        {
            if (!_complete)
            {
                _complete = true;

                var queueClient = Account.CreateCloudQueueClient();

                await CreateQueue(queueClient, CantonConstants.GalleryPagesQueue);
                await CreateQueue(queueClient, CantonConstants.CatalogCommitQueue);
                await CreateQueue(queueClient, CantonConstants.CatalogPageQueue);

                var blobClient = Account.CreateCloudBlobClient();
                await CreateContainer(blobClient, Config.GetProperty("CatalogContainer"));
                await CreateContainer(blobClient, Config.GetProperty("RegistrationContainer"));
                await CreateContainer(blobClient, Config.GetProperty("GalleryPageContainer"));
                await CreateContainer(blobClient, Config.GetProperty("tmp"));

                var tableClient = Account.CreateCloudTableClient();
                var cursorTable = tableClient.GetTableReference(CantonConstants.CursorTable);
                await cursorTable.CreateIfNotExistsAsync();

                string localTmp = Config.GetProperty("localtmp");
                DirectoryInfo tmpDir = new DirectoryInfo(localTmp);
                if (!tmpDir.Exists)
                {
                    tmpDir.Create();
                }
            }
        }

        private async Task CreateContainer(CloudBlobClient client, string name)
        {
            var container = client.GetContainerReference(name);
            await container.CreateIfNotExistsAsync();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        private async Task CreateQueue(CloudQueueClient client, string queueName)
        {
            var queue = client.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
        }
    }
}
