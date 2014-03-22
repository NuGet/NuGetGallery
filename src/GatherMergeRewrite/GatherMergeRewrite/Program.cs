using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    class Program
    {
        static void Upload(string ownerId, string registrationId, string nupkg, DateTime published)
        {
            UploadData data = new UploadData
            {
                OwnerId = ownerId,
                RegistrationId = registrationId,
                Nupkg = nupkg,
                Published = published
            };

            Processor.Upload(data);
        }

        static void Main(string[] args)
        {
            Config.Container = "pub";
            Config.BaseAddress = "http://nuget3.blob.core.windows.net";
            Config.ConnectionString = "";

            //Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.1.10311.nupkg", DateTime.Now);
            //Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.1.10715.nupkg", DateTime.Now);
            //Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.2.0.nupkg", DateTime.Now);
            //Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.3.1.nupkg", DateTime.Now);

            //Upload("microsoft", "dotnetrdf", @"c:\data\nupkgs\dotNetRDF.0.5.0.nupkg", DateTime.Now);
            //Upload("microsoft", "dotnetrdf", @"c:\data\nupkgs\dotNetRDF.0.8.0.nupkg", DateTime.Now);
            Upload("microsoft", "dotnetrdf", @"c:\data\nupkgs\dotNetRDF.1.0.3.nupkg", DateTime.Now);
        }
    }
}
