
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
namespace NuGet.Services.Publish
{
    public class PackageIdentity
    {
        public string Namespace { get; set; }
        public string Id { get; set; }
        public SemanticVersion Version { get; set; }

        public static PackageIdentity FromCatalogEntry(JObject catalogEntry)
        {
            return new PackageIdentity
            {
                Namespace = catalogEntry["namespace"].ToString(),

                //BUG catalog should contain just the simple id not <namespace>/<id>
                Id = catalogEntry["originalId"].ToString(),
                Version = SemanticVersion.Parse(catalogEntry["version"].ToString())
            };
        }
    }
}