
namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackagePipelineFactory
    {
        public static PackagePipeline Create()
        {
            PackagePipeline pipeline = new PackagePipeline();

            pipeline.Add(new NuspecStage());
            pipeline.Add(new PackageHashStage());
            pipeline.Add(new PackageEntriesStage());
            pipeline.Add(new PublicationDetailsStage());
            pipeline.Add(new CommitDetailsStage());
            //  etc

            return pipeline;
        }
    }
}
