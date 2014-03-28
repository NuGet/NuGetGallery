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
        static void Upload(string ownerId, string registrationId, string nupkg, DateTime published)
        {
            LocalPackageHandle handle = new LocalPackageHandle(ownerId, registrationId, nupkg, published);

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

            Processor.UploadPackage(handle, storage).Wait();
        }

        static void Main(string[] args)
        {
            string owner = "microsoft";
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
                    Upload(owner, registrationId, nupkg.FullName, DateTime.Now);
                    packages++;
                }
            }

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages);

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
