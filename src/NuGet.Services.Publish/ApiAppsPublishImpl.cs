// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        ICategorizationPermission _categorizationPermission;
        Uri _imagesUri;

        public ApiAppsPublishImpl(IRegistrationOwnership registrationOwnership, ICategorizationPermission categorizationPermission, Uri imagesUri)
            : base(registrationOwnership)
        {
            _categorizationPermission = categorizationPermission;
            _imagesUri = imagesUri;
        }

        public ApiAppsPublishImpl(IRegistrationOwnership registrationOwnership)
            : base(registrationOwnership)
        {
        }

        async Task<bool> IsAllowedToSpecifyCategory(string id)
        {
            return (_categorizationPermission == null) ? false : await _categorizationPermission.IsAllowedToSpecifyCategory(id);
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

        protected override async Task GenerateNuspec(IDictionary<string, JObject> metadata)
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

            JToken jtokenCategories;
            if (await IsAllowedToSpecifyCategory(id) && apiapp.TryGetValue("categories", out jtokenCategories))
            {
                if (jtokenCategories is JArray)
                {
                    nuspec["categories"] = jtokenCategories;
                }
                else
                {
                    nuspec["categories"] = new JArray(jtokenCategories.ToString());
                }
            }
            else
            {
                nuspec["categories"] = new JArray("community");
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

            AddDefaultEntries((JArray)nuspec["entries"]);

            metadata["nuspec"] = nuspec;
        }

        void AddDefaultEntries(JArray entries)
        {
            HashSet<string> fullNames = new HashSet<string>();
            foreach (JObject entry in entries)
            {
                JToken fullName = entry["fullName"];

                if (fullName != null)
                {
                    fullNames.Add(fullName.ToString());
                }
            }

            AddDefault(entries, fullNames, "content/metadata/icons/large.png", "/apiapps/large.png");
            AddDefault(entries, fullNames, "content/metadata/icons/medium.png", "/apiapps/medium.png");
            AddDefault(entries, fullNames, "content/metadata/icons/small.png", "/apiapps/small.png");
            AddDefault(entries, fullNames, "content/metadata/icons/wide.png", "/apiapps/wide.png");
        }

        void AddDefault(JArray entries, HashSet<string> fullNames, string fullname, string relativeAddress)
        {
            if (!fullNames.Contains(fullname))
            {
                entries.Add(new JObject
                {
                    { "fullName", fullname },
                    { "location", new Uri(_imagesUri.AbsoluteUri + relativeAddress).AbsoluteUri }
                });
            }
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

                    if (fullName.StartsWith("content/metadata/screenshots"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.Screenshot, Schema.Prefixes.NuGet);
                    }

                    if (fullName.StartsWith("content/metadata/icons"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.Icon, Schema.Prefixes.NuGet);
                    }

                    if (fullName.StartsWith("content/metadata/deploymentTemplates"))
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.CsmTemplate, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "content/metadata/icons/hero.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.HeroIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "content/metadata/icons/large.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.LargeIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "content/metadata/icons/medium.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.MediumIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "content/metadata/icons/small.png")
                    {
                        MetadataHelpers.AssertType(entry, Schema.DataTypes.SmallIcon, Schema.Prefixes.NuGet);
                    }

                    if (fullName == "content/metadata/icons/wide.png")
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

            //CheckRequiredFile(packageStream, result.Errors, "content/metadata/icons/small-icon.png");
            //CheckRequiredFile(packageStream, result.Errors, "content/metadata/icons/medium-icon.png");
            //CheckRequiredFile(packageStream, result.Errors, "content/metadata/icons/large-icon.png");
            //CheckRequiredFile(packageStream, result.Errors, "content/metadata/icons/hero-icon.png");
            //CheckRequiredFile(packageStream, result.Errors, "content/metadata/icons/wide-icon.png");

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
                    artifacts.Add("package", new PackageArtifact { PackageEntry = entry });
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
                    // todo: there is an odd case where entries without location are present - circumvent it for now
                    if (entry["location"] != null)
                    {
                        artifacts.Add(fullname, new PackageArtifact {Location = entry["location"].ToString()});
                    }
                }
            }

            if (apiappLocation == null)
            {
                throw new Exception(string.Format("unable to find '{0}' file for existing package", ApiAppMetadata));
            }

            //  load of existing apiapp.json

            JObject apiapp;

            Stream apiappStream = await Artifacts.LoadFile(apiappLocation, storagePrimary);
            using (StreamReader reader = new StreamReader(apiappStream))
            {
                apiapp = JObject.Parse(reader.ReadToEnd());
            }

            //  the apiapp file will be needed later for the generation of the catalogEntry

            metadata[ApiAppMetadata] = apiapp;

            //  apply changes

            bool requiresRewrittenApiApp = false;

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

                if (property.Name == "listed")
                {
                    continue;
                }

                apiapp[property.Name] = property.Value;
                requiresRewrittenApiApp = true;
            }

            if (requiresRewrittenApiApp)
            {
                MemoryStream newApiappStream = new MemoryStream();
                StreamWriter writer = new StreamWriter(newApiappStream);
                writer.Write(apiapp.ToString());
                writer.Flush();
                newApiappStream.Seek(0, SeekOrigin.Begin);

                artifacts.Add(ApiAppMetadata, new PackageArtifact { Stream = newApiappStream });
            }
            else
            {
                //  no changes were applied to the apiapp file and so the catalogEntry can just point at the existing one

                artifacts.Add(ApiAppMetadata, new PackageArtifact { Location = apiappLocation });
            }

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