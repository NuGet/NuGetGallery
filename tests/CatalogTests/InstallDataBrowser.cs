using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class InstallDataBrowser
    {
        public static void Test0()
        {
            StorageCredentials credentials = new StorageCredentials("", "");

            CloudStorageAccount account = new CloudStorageAccount(credentials, true);

            CloudBlobClient client = account.CreateCloudBlobClient();

            CloudBlobContainer container = client.GetContainerReference("ver36");

            CloudBlockBlob blob = container.GetBlockBlobReference("index.html");

            blob.Properties.ContentType = "text/html";
            blob.Properties.CacheControl = "no-store";

            blob.UploadFromFile("DataBrowser\\index.html", System.IO.FileMode.Open);

            Console.WriteLine(blob.Uri);
        }
    }
}
