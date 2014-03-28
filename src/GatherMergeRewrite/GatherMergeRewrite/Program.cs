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

            //IStorage storage = new AzureStorage();
            IStorage storage = new FileStorage(@"c:\data\site\pub");

            Processor.UploadPackage(handle, storage).Wait();

            //Console.WriteLine(nupkg);
        }

        static void Main(string[] args)
        {
            //TODO: these should all be attributes of the store - with Container and BaseAddress being properties on IStorage
            Config.Container = "pub";
            //Config.BaseAddress = "http://nuget3.blob.core.windows.net";
            Config.BaseAddress = "http://localhost:8000";
            Config.ConnectionString = "";

            string owner = "microsoft";

            string path = @"c:\data\nupkgs\";

            DateTime before = DateTime.Now;
            int packages = 0;

            DirectoryInfo nupkgs = new DirectoryInfo(path);
            foreach (DirectoryInfo registration in nupkgs.EnumerateDirectories())
            {
                string registrationId = registration.Name.ToLowerInvariant();

                Console.WriteLine(registrationId);

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
