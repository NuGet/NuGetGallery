using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace BulkUpload
{
    class Program
    {
        static string ParseArguments(string[] args, string name, string defaultValue = null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name)
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                }
            }
            if (defaultValue == null)
            {
                throw new ArgumentException(string.Format("required argument {0} was not provided", name));
            }
            return defaultValue;
        }

        public static string GetRegistrationId(FileInfo fileInfo)
        {
            using (Stream stream = fileInfo.OpenRead())
            {
                Package package = Package.Open(stream);
                foreach (PackagePart part in package.GetParts())
                {
                    string s = part.Uri.ToString();

                    if (s.EndsWith(".nuspec"))
                    {
                        string id = s.Substring(1, s.Length - 8);
                        return id.ToLowerInvariant();
                    }
                }
                throw new Exception("unable to find nuspec in package");
            }
        }

        static long TotalBytes = 0;

        static async Task Upload(FileInfo fileInfo, string registrationId, string owner, string connectionString, string containerName)
        {
            string blobName = fileInfo.Name;
            long size = fileInfo.Length;

            using (Stream stream = fileInfo.OpenRead())
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

                blockBlob.Properties.ContentType = "application/octet-stream";

                blockBlob.Metadata["registrationId"] = registrationId;
                blockBlob.Metadata["owner"] = owner;
                blockBlob.Metadata["published"] = DateTime.UtcNow.ToString();

                await blockBlob.UploadFromStreamAsync(stream);

                Console.WriteLine("uploaded {0} size {1:N0} bytes", blobName, size);

                Interlocked.Add(ref TotalBytes, size);
            }
        }

        static void PrintException(Exception e)
        {
            Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
            if (e.InnerException != null)
            {
                PrintException(e.InnerException);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                string path = ParseArguments(args, "-path");
                string owner = ParseArguments(args, "-owner", "nuget");
                string connectionString = ParseArguments(args, "-connection");
                string containerName = ParseArguments(args, "-container");

                ActionBlock<FileInfo> actionBlock = new ActionBlock<FileInfo>(async (fileInfo) =>
                {
                    string registrationId = GetRegistrationId(fileInfo);
                    owner = owner.ToLowerInvariant();

                    await Upload(fileInfo, registrationId, owner, connectionString, containerName);
                },
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 64 });

                DateTime before = DateTime.Now;

                int count = 0;

                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles())
                {
                    actionBlock.Post(fileInfo);

                    count++;
                }

                actionBlock.Complete();
                actionBlock.Completion.Wait();

                DateTime after = DateTime.Now;

                Console.WriteLine("{0} files  {1:N0} bytes  {2} seconds", count, TotalBytes, (after - before).TotalSeconds);
            }
            catch (AggregateException g)
            {
                foreach (Exception e in g.InnerExceptions)
                {
                    PrintException(e);
                }
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}
