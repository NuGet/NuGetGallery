using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    class Program
    {
        static void Upload(IStorage storage, string ownerId, string registrationId, string nupkg, DateTime published)
        {
            LocalPackageHandle[] handles = new LocalPackageHandle[] { new LocalPackageHandle(ownerId, registrationId, nupkg, published) };
            Processor.UploadPackage(handles, storage).Wait();
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
                    Upload(storage, ownerId, registrationId, nupkg.FullName, DateTime.Now);
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
            string registrationId = "entityframework";

            string path = @"c:\data\nupkgs\" + registrationId + @"\";

            DateTime before = DateTime.Now;

            LocalPackageHandle[] handles = new LocalPackageHandle[]
            { 
                new LocalPackageHandle(ownerId, registrationId, path + "entityframework.4.1.10311.nupkg", DateTime.Now),
                new LocalPackageHandle(ownerId, registrationId, path + "EntityFramework.5.0.0.nupkg", DateTime.Now),
            };
            
            Processor.UploadPackage(handles, storage).Wait();

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

            LocalPackageHandle[] handles = batch.Select((item) => new LocalPackageHandle(ownerId, item.Item1, item.Item2, published)).ToArray();
            
            Processor.UploadPackage(handles, storage).Wait();
        }

        static void Test2()
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

                if (batch.Count == 100)
                {
                    BatchUpload(storage, ownerId, batch, DateTime.Now);
                    batch.Clear();
                }
            }
            BatchUpload(storage, ownerId, batch, DateTime.Now);

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages.Count);

            Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
        }

        static void Main(string[] args)
        {
            try
            {
                //Test0();
                //Test1();
                Test2();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
