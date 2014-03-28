using JsonLD.Core;
using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class AzureStorage : IStorage
    {
        //  save
       
        public async Task Save(string contentType, string name, string content)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Config.ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Config.Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Config.Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = "no-store";  // no for production, just helps with debugging

            await blob.UploadTextAsync(content);

            Console.WriteLine("save: {0}", name);
        }

        //  load

        public async Task<string> Load(string name)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Config.ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Config.Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Config.Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                string content = await blob.DownloadTextAsync();
                return content;
            }

            return null;
        }
    }
}
