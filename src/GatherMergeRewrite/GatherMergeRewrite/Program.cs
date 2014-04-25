using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    class Program
    {
        static void Upload(IStorage storage, IList<string> ownerIds, string registrationId, string nupkg, DateTime published)
        {
            LocalPackageHandle[] handles = new LocalPackageHandle[] { new LocalPackageHandle(ownerIds, registrationId, nupkg, published) };
            Processor.Upload(handles, storage).Wait();
        }

        static void Upload(IStorage storage, IList<string> ownerIds, string registrationId, Stream nupkg, DateTime published)
        {
            CloudPackageHandle[] handles = new CloudPackageHandle[] { new CloudPackageHandle(nupkg, ownerIds, registrationId, published) };
            Processor.Upload(handles, storage).Wait();
        }

        static void Test0()
        {
            //IStorage storage = new AzureStorage
            //{
            //    ConnectionString = "",
            //    Container = "pub",
            //    BaseAddress = "http://nuget3.blob.core.windows.net"
            //};

            IStorage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000"
            };

            string ownerId = "microsoft";
            string path = @"c:\data\nupkgs\";

            DateTime before = DateTime.Now;
            int packages = 0;

            DirectoryInfo nupkgs = new DirectoryInfo(path);
            foreach (DirectoryInfo registration in nupkgs.EnumerateDirectories())
            {
                string registrationId = registration.Name.ToLowerInvariant();

                Console.WriteLine(registrationId);

                //if (registrationId != "dotnetrdf") continue;

                foreach (FileInfo nupkg in registration.EnumerateFiles("*.nupkg"))
                {
                    Upload(storage, new List<string> {ownerId}, registrationId, nupkg.FullName, DateTime.Now);
                    packages++;
                }
            }

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages);

            Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
        }

        static void Test1()
        {
            //IStorage storage = new AzureStorage
            //{
            //    ConnectionString = "",
            //    Container = "pub",
            //    BaseAddress = "http://nuget3.blob.core.windows.net"
            //};

            IStorage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000"
            };

            string ownerId = "microsoft";
            string registrationId = "dotnetrdf";

            string path = @"c:\data\nupkgs\" + registrationId + @"\";

            DateTime before = DateTime.Now;

            LocalPackageHandle[] handles = new LocalPackageHandle[]
            { 
                new LocalPackageHandle(new List<string>{ownerId}, registrationId, path + "dotnetrdf.0.8.0.nupkg", DateTime.Now),
                //new LocalPackageHandle(ownerId, registrationId, path + "EntityFramework.5.0.0.nupkg", DateTime.Now),
            };
            
            Processor.Upload(handles, storage).Wait();

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds ", (after - before).TotalSeconds);

            Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
        }

        static void BatchUpload(IStorage storage, string ownerId, List<Tuple<string, string>> batch, DateTime published)
        {
            foreach (Tuple<string, string> t in batch)
            {
                Console.WriteLine(t.Item1);
            }

            LocalPackageHandle[] handles = batch.Select((item) => new LocalPackageHandle(new List<string>{ownerId}, item.Item1, item.Item2, published)).ToArray();
            
            Processor.Upload(handles, storage).Wait();
        }

        static void Test2()
        {
            string accountKey = "";

            IStorage storage = new AzureStorage
            {
                ConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName=nuget3;AccountKey={0}", accountKey),
                Container = "pub",
                BaseAddress = "http://nuget3.blob.core.windows.net"
            };

            //IStorage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\pub",
            //    Container = "pub",
            //    BaseAddress = "http://localhost:8000"
            //};

            string ownerId = "microsoft";
            string path = @"c:\data\nupkgs\";

            const int BatchSize = 100;

            DateTime before = DateTime.Now;

            List<Tuple<string, string>> packages = new List<Tuple<string, string>>();

            DirectoryInfo nupkgs = new DirectoryInfo(path);
            foreach (DirectoryInfo registration in nupkgs.EnumerateDirectories())
            {
                string registrationId = registration.Name.ToLowerInvariant();

                foreach (FileInfo nupkg in registration.EnumerateFiles("*.nupkg"))
                {
                    packages.Add(new Tuple<string, string>(registrationId, nupkg.FullName));
                }
            }

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
            BatchUpload(storage, ownerId, batch, DateTime.Now);

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages.Count);

            if (storage is FileStorage)
            {
                Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
            }
        }

        static async Task Test3_LoadFromDatabaseLogs(string resumePackage)
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


        static void PrintException(Exception e)
        {
            Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
            Console.WriteLine("{0}", e.StackTrace);
            if (e.InnerException != null)
            {
                PrintException(e.InnerException);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                //Test0();

                //Test1();
                
                Test2();
                
                //Test3_LoadFromDatabaseLogs(args.Length > 0 ? args[0] : null).Wait();
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

            //  the following packages are required by the resolver test case...

            //dotnetrdf                                   
            //htmlagilitypack                             
            //newtonsoft.json                             
            //vds.common                                  
            //json-ld.net                                 
            //microsoft.data.edm                          
            //microsoft.data.odata                        
            //microsoft.data.services.client              
            //microsoft.windowsazure.configurationmanager
            //system.spatial                              
            //windowsazure.storage                        
            //aspnet.suppressformsredirect                
            //httpclient                                  
            //jsonvalue                                   
            //webactivator                                
            //webapi                                      
            //webapi.all                                  
            //webapi.core                                 
            //webapi.enhancements
            //webapi.odata
            //microsoft.web.infrastructure
        }
    }
}
