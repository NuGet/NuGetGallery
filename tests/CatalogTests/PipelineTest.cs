using NuGet.Services.Metadata.Catalog.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    static class PipelineTest
    {
        public static void Test0()
        {
            PackagePipeline pipeline = PackagePipelineFactory.Create2();

            Stream stream = new FileStream(@"C:\data\ema\nupkgs\BasicAppPackage.1.0.0.nupkg", FileMode.Open);

            PipelinePackage package = new PipelinePackage(stream);

            PackagePipelineContext context = new PackagePipelineContext(new Uri("http://azure.com/siena/package"));

            pipeline.Execute(package, context);

            PackageMetadataBase result = context.Result;

            Console.WriteLine(result.ToContent());
        }
    }
}
