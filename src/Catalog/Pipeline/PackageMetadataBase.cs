using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public abstract class PackageMetadataBase
    {
        public abstract void Merge(PackageMetadataBase other);
        public abstract JToken ToContent(JObject frame = null);
    }
}
