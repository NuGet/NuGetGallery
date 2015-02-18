using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Services.Publish
{
    public class ApiAppsPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "apiapp.json" };

        public ApiAppsPublishImpl(IRegistrationOwnership registrationOwnership)
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
            JObject apiapp = metadata["apiapp.json"];

            string domain;
            JToken jtokenDomain;
            if (apiapp.TryGetValue("domain", out jtokenDomain))
            {
                domain = jtokenDomain.ToString();
            }
            else
            {
                domain = "nuget.org";
            }

            string id = apiapp["id"].ToString();
            string version = NuGetVersion.Parse(apiapp["version"].ToString()).ToNormalizedString();

            string s = string.Format("http://{0}/{1}/{2}", domain, id, version);

            Uri jsonLdId = new Uri(s.ToLowerInvariant());

            JObject nuspec = new JObject
            {
                { "@id", jsonLdId.ToString() },
                { "@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } } }
            };

            foreach (JProperty property in apiapp.Properties())
            {
                nuspec.Add(property.Name, property.Value);
            }

            if (!apiapp.TryGetValue("domain", out jtokenDomain))
            {
                nuspec.Add("domain", domain);
            }

            JToken jtokenCategory;
            if (!apiapp.TryGetValue("category", out jtokenCategory))
            {
                nuspec.Add("category", new JArray("other"));
            }

            string publisher = domain.Replace("-", "--").Replace(".", "-");

            nuspec.Add("publisher", publisher);

            JObject inventory;
            if (metadata.TryGetValue("inventory", out inventory))
            {
                nuspec.Add("entries", inventory["entries"]);
            }

            metadata["nuspec"] = nuspec;
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.ApiAppPackage;
        }

        protected override void InferArtifactTypes(IDictionary<string, JObject> metadata)
        {
            JObject inventory;
            if (!metadata.TryGetValue("inventory", out inventory))
            {
                return;
            }

            foreach (JObject entry in inventory["entries"])
            {
                JToken fullNameToken;
                if (entry.TryGetValue("fullName", out fullNameToken))
                {
                    string fullName = fullNameToken.ToString();

                    if (fullName.StartsWith("metadata/screenshots"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.Screenshot, Schema.Prefixes.NuGet);
                    }

                    if (fullName.StartsWith("metadata/icons"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.Icon, Schema.Prefixes.NuGet);
                    }

                    if (fullName.StartsWith("metadata/deploymentTemplates"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.CsmTemplate, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "metadata/icons/hero-icon.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.HeroIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "metadata/icons/large-icon.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.LargeIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "metadata/icons/medium-icon.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.MediumIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "metadata/icons/small-icon.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.SmallIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "metadata/icons/wide-icon.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.WideIcon, Schema.Prefixes.NuGet);
                    }
                }
            }
        }

        protected override string Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            if (metadata.Count == 0)
            {
                return "no metadata was found in the package";
            }

            JObject nuspec;
            if (!metadata.TryGetValue("apiapp.json", out nuspec))
            {
                return "apiapp.json was found in the package";
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