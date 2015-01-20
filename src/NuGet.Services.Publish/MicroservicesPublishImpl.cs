using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Services.Publish
{
    public class MicroservicesPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "nuspec.json" };

        protected override bool IsMetadataFile(string fullName)
        {
            return Files.Contains(fullName);
        }

        protected override JObject CreateMetadataObject(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            JObject obj = JObject.Parse(reader.ReadToEnd());
            return obj;
        }

        protected override bool Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            //TODO: add validation
            return true;
        }
    }
}