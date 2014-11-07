
namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public abstract class PackagePipelineStage
    {
        public abstract bool Execute(PipelinePackage package, PackagePipelineContext context);
    }
}
