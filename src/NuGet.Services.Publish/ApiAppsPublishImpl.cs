using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

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

        protected override IList<string> Validate(Stream packageStream)
        {
            IList<string> errors = new List<string>();

            JObject apiapp = GetJObject(packageStream, "apiapp.json");

            if (apiapp == null)
            {
                errors.Add("required file 'apiapp.json' is missing from package");
            }
            else
            {
                JToken id;
                if (!apiapp.TryGetValue("id", out id))
                {
                    errors.Add("required property 'id' is missing from 'apiapp.json' file");
                }

                JToken version;
                if (!apiapp.TryGetValue("version", out version))
                {
                    errors.Add("required property 'version' is missing from 'apiapp.json' file");
                }
            }

            //CheckRequiredFile(packageStream, errors, "metadata/icons/small-icon.png");

            if (errors.Count == 0)
            {
                return null;
            }

            return errors;
        }

        static void CheckRequiredFile(Stream packageStream, IList<string> errors, string fullName)
        {
            if (!FileExists(packageStream, fullName))
            {
                errors.Add(string.Format("required file '{0}' was missing from package", fullName));
            }
        }

        static JObject GetJObject(Stream packageStream, string fullName)
        {
            using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry zipEntry in archive.Entries)
                {
                    if (zipEntry.FullName == fullName)
                    {
                        string s = new StreamReader(zipEntry.Open()).ReadToEnd();
                        return JObject.Parse(s);
                    }
                }
            }
            return null;
        }

        static bool FileExists(Stream packageStream, string fullName)
        {
            using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry zipEntry in archive.Entries)
                {
                    if (zipEntry.FullName == fullName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}