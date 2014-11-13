using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage;
using SimpleGalleryLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleGalleryWebJob
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Running");

            CloudStorageAccount account = CloudStorageAccount.Parse(SimpleGalleryAPI.ConnectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference("upload-0");

            while (true)
            {
                foreach (var blob in container.ListBlobs(string.Empty).Select(i => client.GetBlobReferenceFromServer(i.Uri)))
                {
                    Console.WriteLine("Processing: " + blob.Uri.AbsoluteUri);

                    try
                    {
                        using (var stream = new MemoryStream())
                        {
                            blob.DownloadToStream(stream);
                            stream.Seek(0, SeekOrigin.Begin);

                            SimpleGalleryAPI.AddPackage(account, stream);
                        }
                    }
                    finally
                    {
                        Console.WriteLine("Deleting: " + blob.Uri.AbsoluteUri);
                        blob.Delete();
                    }
                }

                Thread.Sleep(1000 * 5);
            }
        }
    }
}
