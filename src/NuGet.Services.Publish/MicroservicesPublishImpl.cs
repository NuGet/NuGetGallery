using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Services.Publish
{
    public class MicroservicesPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "microservice.json" };

        public MicroservicesPublishImpl(IRegistrationOwnership registrationOwnership)
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

        protected override void GenerateNuspec(IDictionary<string, JObject> metadata)
        {
            JObject microservice = metadata["microservice.json"];

            string id = microservice["id"].ToString();
            string version = NuGetVersion.Parse(microservice["version"].ToString()).ToNormalizedString();

            Uri jsonLdId = new Uri("http://" + id + "/" + version);

            JObject nuspec = new JObject
            {
                { "@id", jsonLdId.ToString() },
                { "@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } } }
            };

            foreach (JProperty property in microservice.Properties())
            {
                nuspec.Add(property.Name, property.Value);
            }

            metadata["nuspec.json"] = nuspec;
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.MicroservicePackage;
        }

        protected override string Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            if (metadata.Count == 0)
            {
                return "no metadata was found in the package";
            }

            JObject nuspec;
            if (!metadata.TryGetValue("microservice.json", out nuspec))
            {
                return "microservice.json was found in the package";
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