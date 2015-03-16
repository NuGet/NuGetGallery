
using NuGet.Versioning;
namespace NuGet.Services.Publish
{
    public class PackageIdentity
    {
        public string Namespace { get; set; }
        public string Id { get; set; }
        public SemanticVersion Version { get; set; }
    }
}