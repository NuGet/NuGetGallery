using Newtonsoft.Json.Linq;
using System;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    class PackageDetailsStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            JToken packageSpec = PackagePipelineHelpers.GetPackageSpec(package, context);

            JToken token = PackagePipelineHelpers.GetContext("");

            Console.WriteLine(token);

            return true;
        }
    }
}
