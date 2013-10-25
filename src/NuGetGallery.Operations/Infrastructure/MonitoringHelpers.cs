using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Infrastructure
{
    public static class MonitoringHelpers
    {
        public static void Append2Blob(string storageConnectionString, string container, string name, int maxDepth, JObject newEntry)
        {
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(container);

            cloudBlobContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob);

            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(name);

            JArray report;

            if (cloudBlockBlob.Exists())
            {
                string json = cloudBlockBlob.DownloadText();

                report = JArray.Parse(json);

                if (report.Count > maxDepth)
                {
                    JArray newReport = new JArray();
                    int j = 0;
                    for (int i = report.Count - maxDepth; i < report.Count; i++)
                    {
                        j++;
                        newReport.Add(report[j]);
                    }
                    report = newReport;
                }
            }
            else
            {
                report = new JArray();
            }

            report.Add(newEntry);

            cloudBlockBlob.Properties.ContentType = "application/json";
            cloudBlockBlob.Properties.CacheControl = "no-cache";

            cloudBlockBlob.UploadText(report.ToString());
        }

        //  these functions make the code above look like it is using the very latest Storage API - delete these when we do

        private static void CreateIfNotExists(this CloudBlobContainer cloudBlobContainer, BlobContainerPublicAccessType access)
        {
            cloudBlobContainer.CreateIfNotExists();
            cloudBlobContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        private static string DownloadText(this CloudBlockBlob cloudBlockBlob)
        {
            MemoryStream stream = new MemoryStream();
            cloudBlockBlob.DownloadToStream(stream);
            stream.Seek(0, SeekOrigin.Begin);
            using (TextReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static void UploadText(this CloudBlockBlob cloudBlockBlob, string text)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(text);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            cloudBlockBlob.UploadFromStream(stream);
        }
    }
}
