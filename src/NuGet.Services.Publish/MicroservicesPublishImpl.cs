using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class MicroservicesPublishImpl : PublishImpl
    {
        static ISet<string> Files = new HashSet<string> { "apiapp.json" };

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
            JObject microservice = metadata["apiapp.json"];

            string domain;
            JToken jtokenDomain;
            if (microservice.TryGetValue("domain", out jtokenDomain))
            {
                domain = jtokenDomain.ToString();
            }
            else
            {
                domain = "nuget.org";
            }

            string id = microservice["id"].ToString();
            string version = NuGetVersion.Parse(microservice["version"].ToString()).ToNormalizedString();

            string s = string.Format("http://{0}/{1}/{2}", domain, id, version);

            Uri jsonLdId = new Uri(s.ToLowerInvariant());

            JObject nuspec = new JObject
            {
                { "@id", jsonLdId.ToString() },
                { "@context", new JObject { { "@vocab", "http://schema.nuget.org/schema#" } } }
            };

            foreach (JProperty property in microservice.Properties())
            {
                nuspec.Add(property.Name, property.Value);
            }

            if (!microservice.TryGetValue("domain", out jtokenDomain))
            {
                nuspec.Add("domain", domain);
            }

            JToken jtokenCategory;
            if (!microservice.TryGetValue("category", out jtokenCategory))
            {
                nuspec.Add("category", new JArray("apiapp"));
            }

            string publisher = domain.Replace("-", "--").Replace(".", "-");

            nuspec.Add("publisher", publisher);

            metadata["nuspec.json"] = nuspec;
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.MicroservicePackage;
        }

        protected override async Task ExtractAndSavePackageArtifacts(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            JObject icons = new JObject();

            using (ZipArchive archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName == "metadata/icons/hero-icon.png")
                    {
                        icons["hero"] = (await SaveIcon(entry, metadata, "hero")).ToString();
                    }
                    if (entry.FullName == "metadata/icons/large-icon.png")
                    {
                        icons["large"] = (await SaveIcon(entry, metadata, "large")).ToString();
                    }
                    if (entry.FullName == "metadata/icons/medium-icon.png")
                    {
                        icons["medium"] = (await SaveIcon(entry, metadata, "medium")).ToString();
                    }
                    if (entry.FullName == "metadata/icons/small-icon.png")
                    {
                        icons["small"] = (await SaveIcon(entry, metadata, "small")).ToString();
                    }
                    if (entry.FullName == "metadata/icons/wide-icon.png")
                    {
                        icons["wide"] = (await SaveIcon(entry, metadata, "wide")).ToString();
                    }
                }
            }

            metadata["nuspec.json"]["icons"] = icons;
        }

        static string GetIconName(IDictionary<string, JObject> metadata, string size)
        {
            return string.Format("{0}.{1}.{2}.{3}.{4}.png",
                GetDomain(metadata),
                GetId(metadata),
                GetVersion(metadata),
                size,
                Guid.NewGuid()).ToLowerInvariant();
        }

        async Task<Uri> SaveIcon(ZipArchiveEntry entry, IDictionary<string, JObject> metadata, string size)
        {
            using (Stream stream = entry.Open())
            {
                string name = GetIconName(metadata, size);
                return await SaveFile(stream, name, "image/png");
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