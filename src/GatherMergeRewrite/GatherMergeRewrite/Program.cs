using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    class Program
    {
        static void Main(string[] args)
        {
            Processor.Container = "pub";
            Processor.BaseAddress = "http://nuget3.blob.core.windows.net";
            Processor.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=nuget3;AccountKey=KD6kANHO/jSm3zA4zEl+iNdgfjDkw0dJm3fns8GZOFlH0WunqkO32RbJlTsEq1lrTiQoYxFGiogH7JF8xqdmiw==";

            //Processor.Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.1.10311.nupkg", DateTime.Now);
            //Processor.Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.1.10715.nupkg", DateTime.Now);
            //Processor.Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.2.0.nupkg", DateTime.Now);
            //Processor.Upload("microsoft", "entityframework", @"c:\data\nupkgs\entityframework.4.3.1.nupkg", DateTime.Now);

            Processor.Upload("microsoft", "dotnetrdf", @"c:\data\nupkgs\dotNetRDF.1.0.3.nupkg", DateTime.Now);
        }
    }
}
