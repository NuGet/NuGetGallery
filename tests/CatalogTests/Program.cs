using Catalog;
using Catalog.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CatalogTests
{
    class Program
    {
        static void BatchUpload(IStorage storage, string ownerId, List<Tuple<string, string>> batch, DateTime published)
        {
            foreach (Tuple<string, string> t in batch)
            {
                Console.WriteLine(t.Item1);
            }

            LocalPackageHandle[] handles = batch.Select((item) => new LocalPackageHandle(ownerId, item.Item1, item.Item2, published)).ToArray();

            Processor.Upload(handles, storage).Wait();

            Console.WriteLine("...uploaded");
        }

        static List<Tuple<string, string>> GatherPackageList(string paths)
        {
            List<Tuple<string, string>> packages = new List<Tuple<string, string>>();

            foreach (string path in paths.Split(';'))
            {
                DirectoryInfo nupkgs = new DirectoryInfo(path);
                foreach (DirectoryInfo registration in nupkgs.EnumerateDirectories())
                {
                    string registrationId = registration.Name.ToLowerInvariant();

                    foreach (FileInfo nupkg in registration.EnumerateFiles("*.nupkg"))
                    {
                        packages.Add(new Tuple<string, string>(registrationId, nupkg.FullName));
                    }
                }
            }

            return packages;
        }

        static void Test0()
        {
            IStorage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000"
            };

            string ownerId = "microsoft";
            string path = @"c:\data\nupkgs;c:\data\nupkgs2;c:\data\nupkgs3;c:\data\nupkgs4";

            const int BatchSize = 100;

            DateTime before = DateTime.Now;

            List<Tuple<string, string>> packages = GatherPackageList(path);

            List<Tuple<string, string>> batch = new List<Tuple<string, string>>();
            foreach (Tuple<string, string> item in packages)
            {
                batch.Add(item);

                if (batch.Count == BatchSize)
                {
                    BatchUpload(storage, ownerId, batch, DateTime.Now);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                BatchUpload(storage, ownerId, batch, DateTime.Now);
            }

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages.Count);

            if (storage is FileStorage)
            {
                Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
            }
        }

        static async Task Test1(string resumePackage)
        {
            string connectionString = "";
            IStorage storage = new AzureStorage
            {
                ConnectionString = connectionString,
                Container = "package-metadata",
                BaseAddress = "http://nugetprod1.blob.core.windows.net"
            };

            //IStorage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\pub",
            //    Container = "pub",
            //    BaseAddress = "http://localhost:8000"
            //};

            DateTime before = DateTime.Now;

            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobContainer mdtriggers = account.CreateCloudBlobClient().GetContainerReference("mdtriggers");
            CloudBlobContainer metadata = account.CreateCloudBlobClient().GetContainerReference("package-metadata");
            CloudBlobContainer packages = account.CreateCloudBlobClient().GetContainerReference("packages");
            BlobContinuationToken token = null;

            int packageCount = 0;

            bool resumePoint = resumePackage == null;

            do
            {
                var result = mdtriggers.ListBlobsSegmented(token);
                token = result.ContinuationToken;
                var blobs = result.Results;
                packageCount += result.Results.Count();

                List<CloudPackageHandle> uploads = new List<CloudPackageHandle>();
                List<Task> downloads = new List<Task>();
                List<string> tempFiles = new List<string>();

                foreach (var blob in blobs)
                {
                    if (!resumePoint)
                    {
                        if (blob.Uri.ToString().ToLowerInvariant().Contains(resumePackage))
                        {
                            resumePoint = true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var blobRef = account.CreateCloudBlobClient().GetBlobReferenceFromServer(blob.StorageUri);
                    var stream = blobRef.OpenRead();
                    var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    var obj = JObject.Parse(json);
                    var ownerArray = JArray.Parse((string)obj["Owners"]);

                    string packageId = ((string)obj["PackageId"]).ToLowerInvariant();
                    string versionString = (string)obj["PackageVersion"];

                    string normalizedVersionString = SemanticVersionExtensions.Normalize(versionString);

                    string nupkgName = (packageId + "." + normalizedVersionString + ".nupkg").ToLowerInvariant();
                    string nupkgUrl = "http://nugetprod1.blob.core.windows.net/packages/" + nupkgName;

                    string file = Path.GetTempFileName();
                    tempFiles.Add(file);

                    downloads.Add(Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            await new WebClient().DownloadFileTaskAsync(new Uri(nupkgUrl), file);
                        }
                        catch (WebException)
                        {
                            using (var log = File.AppendText("log.txt"))
                            {
                                log.WriteLine("Couldn't download '{0}'.", nupkgName);
                            }
                            return;
                        }
                        var nupkgStream = File.OpenRead(file);
                        lock (uploads)
                        {
                            uploads.Add(new CloudPackageHandle(nupkgStream, ownerArray.Count() != 0 ? (string)ownerArray[0]["OwnerName"] : "NULL",
                                packageId, DateTime.Now));
                        }
                    }));

                    if (downloads.Count() == 100)
                    {
                        await Task.WhenAll(downloads.ToArray());

                        await Processor.Upload(uploads.ToArray(), storage);

                        lock (uploads)
                        {
                            foreach (var upload in uploads)
                            {
                                upload.Close();
                            }

                            downloads.Clear();
                            uploads.Clear();
                        }
                    }
                }

                if (downloads.Count() != 0)
                {
                    await Task.WhenAll(downloads.ToArray());

                    await Processor.Upload(uploads.ToArray(), storage);

                    foreach (var upload in uploads)
                    {
                        upload.Close();
                    }

                    downloads.Clear();
                    uploads.Clear();
                }

                foreach (string tempfile in tempFiles)
                {
                    File.Delete(tempfile);
                }
                tempFiles.Clear();

            } while (token != null);


            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packageCount);

            Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
        }

        static void Test2()
        {
            string baseAddress = "http://localhost:8000/pub/";
            DateTime since = DateTime.MinValue;

            Collector collector = new Collector();

            collector.Run(baseAddress, since);
        }

        static void PrintException(Exception e)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    PrintException(ex);
                }
            }
            else
            {
                Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
                Console.WriteLine("{0}", e.StackTrace);
                if (e.InnerException != null)
                {
                    PrintException(e.InnerException);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                //Test0();
                //Test1(args.Length > 0 ? args[0] : null).Wait();
                Test2();
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}
