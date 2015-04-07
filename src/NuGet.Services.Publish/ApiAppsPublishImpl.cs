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
    public class ApiAppsPublishImpl : PublishImpl
    {
        const string DefaultPackageNamespace = "nuget.org";
        const string ApiAppMetadata = "content/apiapp.json";

        static ISet<string> Files = new HashSet<string> { ApiAppMetadata };

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
            JObject apiapp = metadata[ApiAppMetadata];

            string ns;
            JToken jtokenNamespace;
            if (apiapp.TryGetValue("namespace", out jtokenNamespace))
            {
                ns = jtokenNamespace.ToString();
            }
            else
            {
                ns = "nuget.org";
            }

            string originalId = apiapp["id"].ToString();

            string id = string.Format("{0}.{1}", ns, originalId);
            string version = NuGetVersion.Parse(apiapp["version"].ToString()).ToNormalizedString();

            string s = string.Format("http://{0}/{1}", id, version);

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

            if (!apiapp.TryGetValue("namespace", out jtokenNamespace))
            {
                nuspec.Add("namespace", ns);
            }

            nuspec["id"] = id;
            nuspec["version"] = version;
            nuspec["originalId"] = originalId; 

            JToken jtokenCategory;
            if (!apiapp.TryGetValue("category", out jtokenCategory))
            {
                nuspec.Add("category", new JArray("other"));
            }

            JToken jtokenDescription;
            if (!apiapp.TryGetValue("description", out jtokenDescription))
            {
                nuspec.Add("description", apiapp["summary"]);
            }

            //TODO: add default icons - and these would be present in the inventory - but flagged somehow

            string marketplacePublisher = ns.Replace("-", "--").Replace(".", "-");
            nuspec.Add("marketplacePublisher", marketplacePublisher);

            string marketplaceName = originalId.Replace("-", "--").Replace(".", "-");
            nuspec.Add("marketplaceName", marketplaceName);

            JObject inventory;
            if (metadata.TryGetValue("inventory", out inventory))
            {
                nuspec.Add("entries", inventory["entries"]);
                nuspec.Add("packageContent", inventory["packageContent"]);
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

        protected override Task<ValidationResult> Validate(Stream packageStream)
        {
            ValidationResult result = new ValidationResult();

            JObject apiapp = GetJObject(packageStream, ApiAppMetadata);

            if (apiapp == null)
            {
                result.Errors.Add(string.Format("required file '{0}' is missing from package", ApiAppMetadata));
            }
            else
            {
                result.PackageIdentity = ValidationHelpers.ValidateIdentity(apiapp, result.Errors);

                //CheckRequiredProperty(apiapp, errors, "description");
                ValidationHelpers.CheckRequiredProperty(apiapp, result.Errors, "title");
                ValidationHelpers.CheckRequiredProperty(apiapp, result.Errors, "summary");
                ValidationHelpers.CheckRequiredProperty(apiapp, result.Errors, "author");
                ValidationHelpers.CheckRequiredProperty(apiapp, result.Errors, "namespace");
            }

            //CheckRequiredFile(packageStream, errors, "metadata/icons/small-icon.png");
            //CheckRequiredFile(packageStream, errors, "metadata/icons/medium-icon.png");
            //CheckRequiredFile(packageStream, errors, "metadata/icons/large-icon.png");
            //CheckRequiredFile(packageStream, errors, "metadata/icons/hero-icon.png");
            //CheckRequiredFile(packageStream, errors, "metadata/icons/wide-icon.png");

            return Task.FromResult(result);
        }

        protected override async Task<IDictionary<string, PackageArtifact>> GenerateNewArtifactsFromEdit(IDictionary<string, JObject> metadata, JObject catalogEntry, JObject editMetadata, string storagePrimary)
        {
            IDictionary<string, PackageArtifact> artifacts = new Dictionary<string, PackageArtifact>();

            IDictionary<string, Stream> newEntries = new Dictionary<string, Stream>();

            JToken entries;
            if (editMetadata.TryGetValue("entries", out entries))
            {
                foreach (JObject entry in entries)
                {
                    string fullname = entries["fullname"].ToString();

                    newEntries.Add(fullname, null);
                }
            }

            // copy existing except those specified in the editMetadata

            string apiappLocation = null;

            foreach (JObject entry in catalogEntry["entries"])
            {
                // package.zip doesn't have a fullName
                JToken fullnameJToken;
                if (!entry.TryGetValue("fullName", out fullnameJToken))
                {
                    continue;
                }

                string fullname = fullnameJToken.ToString();

                if (fullname == ApiAppMetadata)
                {
                    apiappLocation = entry["location"].ToString();
                    continue;
                }

                Stream stream;
                if (newEntries.TryGetValue(fullname, out stream))
                {
                    if (stream != null)
                    {
                        artifacts.Add(fullname, new PackageArtifact { Stream = stream });
                    }
                    // else DELETE
                }
                else
                {
                    artifacts.Add(fullname, new PackageArtifact { Location = entry["location"].ToString() });
                }
            }

            if (apiappLocation == null)
            {
                throw new Exception(string.Format("unable to find '{0}' file for existing package", ApiAppMetadata));
            }

            //  load existing apiapp.json

            JObject apiapp;
            Stream apiappStream = await Artifacts.LoadFile(apiappLocation, storagePrimary);
            using (StreamReader reader = new StreamReader(apiappStream))
            {
                apiapp = JObject.Parse(reader.ReadToEnd());
            }

            //  apply changes

            foreach (JProperty property in editMetadata.Properties())
            {
                if (property.Name == "catalogEntry")
                {
                    continue;
                }

                if (property.Name == "entries")
                {
                    continue;
                }

                apiapp[property.Name] = property.Value;
            }

            MemoryStream newApiappStream = new MemoryStream();
            StreamWriter writer = new StreamWriter(newApiappStream);
            writer.Write(apiapp.ToString());
            writer.Flush();
            newApiappStream.Seek(0, SeekOrigin.Begin);

            metadata[ApiAppMetadata] = apiapp;
            artifacts.Add(ApiAppMetadata, new PackageArtifact { Stream = newApiappStream });

            return artifacts;
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
    }
}