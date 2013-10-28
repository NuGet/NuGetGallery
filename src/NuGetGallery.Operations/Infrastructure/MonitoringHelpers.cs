using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace NuGetGallery.Operations.Infrastructure
{
    public static class MonitoringHelpers
    {
        public static void AppendToBlob(string storageConnectionString, string container, string name, int maxDepth, JObject newEntry)
        {
            int attempts = 0;
            bool execute = true;
            while (execute)
            {
                try
                {
                    InnerAppendToBlob(storageConnectionString, container, name, maxDepth, newEntry);
                    execute = false;
                }
                catch (StorageException storageException)
                {
                    int statusCode = storageException.RequestInformation.HttpStatusCode;
                    if (statusCode == 412  || statusCode == 409)
                    {
                        //  retry for 5 minutes
                        if (attempts++ == 60)
                        {
                            throw;
                        }
                        Thread.Sleep(10 * 1000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public static void InnerAppendToBlob(string storageConnectionString, string container, string name, int maxDepth, JObject newEntry)
        {
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(container);

            cloudBlobContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob);

            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(name);

            JArray report;

            string lease = null;

            if (cloudBlockBlob.Exists())
            {
                lease = cloudBlockBlob.AcquireLease(TimeSpan.FromSeconds(30), null);

                string json = cloudBlockBlob.DownloadText();

                Console.WriteLine("hit enter");
                Console.ReadLine();

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

            if (lease != null)
            {
                cloudBlockBlob.UploadText(report.ToString(), null, new AccessCondition { LeaseId = lease });
                cloudBlockBlob.ReleaseLease(new AccessCondition { LeaseId = lease });
            }
            else
            {
                cloudBlockBlob.UploadText(report.ToString());
            }
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

        private static void UploadText(this CloudBlockBlob cloudBlockBlob, string text, Encoding encoding = null, AccessCondition accessCondition = null)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(text);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            cloudBlockBlob.UploadFromStream(stream, accessCondition);
        }
    }
}
