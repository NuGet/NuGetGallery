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

            string id = string.Format("{0}/{1}", ns, originalId);
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

            JObject apiapp = GetJObject(packageStream, "apiapp.json");

            if (apiapp == null)
            {
                result.Errors.Add("required file 'apiapp.json' is missing from package");
            }
            else
            {
                result.PackageIdentity = ValidateIdentity(apiapp, result.Errors);

                //CheckRequiredProperty(apiapp, errors, "description");
                CheckRequiredProperty(apiapp, result.Errors, "title");
                CheckRequiredProperty(apiapp, result.Errors, "summary");
                CheckRequiredProperty(apiapp, result.Errors, "author");
                CheckRequiredProperty(apiapp, result.Errors, "namespace");
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
                JToken fullnameJToken;
                if (!entry.TryGetValue("fullName", out fullnameJToken))
                {
                    continue;
                }

                string fullname = fullnameJToken.ToString();

                if (fullname == "apiapp.json")
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
                }
                else
                {
                    artifacts.Add(fullname, new PackageArtifact { Location = entry["location"].ToString() });
                }
            }

            if (apiappLocation == null)
            {
                throw new Exception("unable to find apiapp.json file for existing package");
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

            metadata["apiapp.json"] = apiapp;
            artifacts.Add("apiapp.json", new PackageArtifact { Stream = newApiappStream });

            return artifacts;
        }

        static PackageIdentity ValidateIdentity(JObject metadata, IList<string> errors)
        {
            string ns = null;
            string id = null;
            SemanticVersion semanticVersion = null;

            JToken namespaceJToken = CheckRequiredProperty(metadata, errors, "namespace");
            if (namespaceJToken != null)
            {
                ns = namespaceJToken.ToString();
                if (ns.LastIndexOfAny(new[] { '/', '@' }) != -1)
                {
                    errors.Add("'/', '@' characters are not permitted in namespace property");
                }
            }
            else
            {
                ns = DefaultPackageNamespace;
            }

            JToken idJToken = CheckRequiredProperty(metadata, errors, "id");
            if (idJToken != null)
            {
                id = idJToken.ToString();
                if (id.LastIndexOfAny(new[] { '/', '@' }) != -1)
                {
                    errors.Add("'/', '@' characters are not permitted in id property");
                }
            }

            JToken versionJToken = CheckRequiredProperty(metadata, errors, "version");
            if (versionJToken != null)
            {
                string version = versionJToken.ToString();
                if (!SemanticVersion.TryParse(version, out semanticVersion))
                {
                    errors.Add("the version property must follow the Semantic Version rules, refer to 'http://semver.org'");
                }
            }

            return new PackageIdentity { Namespace = ns, Id = id, Version = semanticVersion };
        }

        static JToken CheckRequiredProperty(JObject obj, IList<string> errors, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, out token))
            {
                errors.Add(string.Format("required property '{0}' is missing from 'apiapp.json' file", name));
            }
            return token;
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