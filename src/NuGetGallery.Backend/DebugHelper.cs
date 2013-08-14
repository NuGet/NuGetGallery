using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System;
using System.IO;

namespace NuGetGallery.Backend
{
    internal static class DebugHelper
    {
        public static void WriteDebugBlob(string blobName, string message)
        {
            string destinationAccountName = "";
            string destinationAccessKey = "";
            string destinationContainer = "";

            StorageCredentialsAccountAndKey accountAndKey = new StorageCredentialsAccountAndKey(destinationAccountName, destinationAccessKey);
            CloudStorageAccount account = new CloudStorageAccount(accountAndKey, false);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(destinationContainer);

            CloudBlob blob = container.GetBlobReference(blobName);
            Stream stream = blob.OpenWrite();
            using (TextWriter writer = new StreamWriter(stream))
            {
                writer.Write("{0} {1}", DateTime.UtcNow, message);
            }
        }
    }
}
