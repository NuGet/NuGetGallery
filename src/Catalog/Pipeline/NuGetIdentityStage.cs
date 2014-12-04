using Newtonsoft.Json.Linq;
using System;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    class NuGetIdentityStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            JToken packageSpec = PackagePipelineHelpers.GetPackageSpec(package, context);
            string id = packageSpec["id"].ToString();
            string version = packageSpec["version"].ToString();
            string relativeAddress = string.Format("{0}/{1}", id, version);
            context.Uri = new Uri(context.BaseAddress, relativeAddress.ToLowerInvariant());
            packageSpec["@id"] = context.Uri.AbsoluteUri;
            return true;
        }
    }
}
