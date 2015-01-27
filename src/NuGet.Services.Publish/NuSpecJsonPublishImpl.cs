using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Services.Publish
{
    public class NuSpecJsonPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "nuspec.json" };

        public NuSpecJsonPublishImpl(IRegistrationOwnership registrationOwnership)
            : base(registrationOwnership)
        {
        }

        protected override bool IsMetadataFile(string fullName)
        {
            return Files.Contains(fullName);
        }

        protected override JObject CreateMetadataObject(string fullname, Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            JObject obj = JObject.Parse(reader.ReadToEnd());
            return obj;
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        protected override string Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            if (metadata.Count == 0)
            {
                return "no metadata was found in the package";
            }

            JObject nuspec;
            if (!metadata.TryGetValue("nuspec.json", out nuspec))
            {
                return "nuspec.json was found in the package";
            }

            JToken id;
            if (!nuspec.TryGetValue("id", out id))
            {
                return "required property 'id' was missing from metadata";
            }

            JToken version;
            if (!nuspec.TryGetValue("version", out version))
            {
                return "required property 'version' was missing from metadata";
            }

            return null;
        }
    }
}