using Catalog;
using Catalog.Persistence;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace CatalogTests
{
    class Program
    {
        static async Task Test1(string container, int batchSize, string resumePackage)
        {
            string connectionStringProd = "DefaultEndpointsProtocol=https;AccountName=nugetprod1;AccountKey=";
            string connectionStringDev = "DefaultEndpointsProtocol=https;AccountName=nugetdev1;AccountKey=;";
            IStorage storage = new AzureStorage
            {
                ConnectionString = connectionStringDev,
                Container = container,
                BaseAddress = "http://nugetdev1.blob.core.windows.net"
            };

            //IStorage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\pub",
            //    Container = "pub",
            //    BaseAddress = "http://localhost:8000"
            //};

            DateTime before = DateTime.Now;

            CloudStorageAccount account1 = CloudStorageAccount.Parse(connectionStringProd);
            CloudStorageAccount account2 = CloudStorageAccount.Parse(connectionStringDev);
            CloudBlobContainer mdtriggers = account1.CreateCloudBlobClient().GetContainerReference("mdtriggers");
            CloudBlobContainer metadata = account2.CreateCloudBlobClient().GetContainerReference(container);
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
                        if (blob.Uri.ToString().ToLowerInvariant().Split("/".ToCharArray()).Last().StartsWith(resumePackage + "."))
                        {
                            resumePoint = true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var blobRef = account1.CreateCloudBlobClient().GetBlobReferenceFromServer(blob.StorageUri);
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

                    downloads.Add(await Task.Factory.StartNew(async () =>
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
                        Console.WriteLine("Downloaded {0}", nupkgUrl);
                        lock (uploads)
                        {
                            uploads.Add(new CloudPackageHandle(nupkgStream, ownerArray.Count() != 0 ? ownerArray.Select(o => (string)o["OwnerName"]).ToList() : new List<string> { "NULL" },
                                packageId, DateTime.Now));
                        }
                    }));

                    if (downloads.Count() == batchSize)
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
                DateTime before = DateTime.Now;

                //Test1(args[0], int.Parse(args[1]), args.Length > 2 ? args[2] : null).Wait();
                //BuilderTests.Test0();
                //BuilderTests.Test1();
                //BuilderTests.Test2();

                //CollectorTests.Test0();
                //CollectorTests.Test1();
                //CollectorTests.Test2();
                CollectorTests.Test3();

                DateTime after = DateTime.Now;

                Console.WriteLine("Total duration {0} seconds", (after - before).TotalSeconds);
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}
