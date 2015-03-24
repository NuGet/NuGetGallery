using Newtonsoft.Json.Linq;
using System.IO;

namespace NuGet.Services.Publish
{
    public class PackageArtifact
    {
        public Stream Stream { get; set; }
        public string Location { get; set; }
    }
}