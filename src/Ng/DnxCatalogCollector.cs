// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public class DnxCatalogCollector : CommitCollector
    {
        StorageFactory _storageFactory;

        public DnxCatalogCollector(Uri index, StorageFactory storageFactory, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _storageFactory = storageFactory;
        }

        public Uri ContentBaseAddress { get; set; }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, CancellationToken cancellationToken)
        {
            foreach (JToken item in items)
            {
                string id = item["nuget:id"].ToString().ToLowerInvariant();
                string version = item["nuget:version"].ToString().ToLowerInvariant();
                Uri catalogEntryUri = new Uri(item["@id"].ToString());

                Storage storage = _storageFactory.Create(id);
                string nuspec = await LoadNuspec(id, version, cancellationToken);
                JObject catalogEntry = await client.GetJObjectAsync(catalogEntryUri, cancellationToken);
                bool isListed = IsListed(catalogEntry);

                if (nuspec != null)
                {
                    await SaveNuspec(storage, id, version, nuspec, cancellationToken);
                    await CopyNupkg(storage, id, version, cancellationToken);
                    await UpdateMetadata(storage, version, isListed, cancellationToken);

                    Trace.TraceInformation("commit: {0}/{1}", id, version);
                }
                else
                {
                    Trace.TraceInformation("no nuspec available for {0}/{1} skipping", id, version);
                }
            }

            return true;
        }

        async Task UpdateMetadata(Storage storage, string version, bool isListed, CancellationToken cancellationToken)
        {
            Uri resourceUri = new Uri(storage.BaseAddress, "index.json");
            string json = await storage.LoadString(resourceUri, cancellationToken);
            JObject indexObj = null;
            if (json != null)
            {
                indexObj = JObject.Parse(json);
            }

            HashSet<NuGetVersion> versions = GetVersions(indexObj, "versions");
            HashSet<NuGetVersion> unlistedVersions = GetVersions(indexObj, "unlistedVersions");

            if (isListed)
            {
                versions.Add(NuGetVersion.Parse(version));
            }
            else
            {
                unlistedVersions.Add(NuGetVersion.Parse(version));
            }
     
            await storage.Save(resourceUri, CreateContent(versions, unlistedVersions), cancellationToken);
        }

        async Task<string> LoadNuspec(string id, string version, CancellationToken cancellationToken)
        {
            using (HttpClient client = new HttpClient())
            {
                Uri requestUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));
                HttpResponseMessage httpResponseMessage = await client.GetAsync(requestUri, cancellationToken);
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                    {
                        string nuspec = GetNuspec(stream, id);
                        return nuspec;
                    }
                }
                else if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Trace.TraceInformation("package not found...");
                }
                else
                {
                    httpResponseMessage.EnsureSuccessStatusCode();
                }
                return null;
            }
        }

        async Task SaveNuspec(Storage storage, string id, string version, string nuspec, CancellationToken cancellationToken)
        {
            string relativeAddress = string.Format("{1}/{0}.nuspec", id, version);
            Uri nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            await storage.Save(nuspecUri, new StringStorageContent(nuspec, "text/xml"), cancellationToken);
        }

        static HashSet<NuGetVersion> GetVersions(JObject indexObj, string propertyName)
        {
            HashSet<NuGetVersion> result = new HashSet<NuGetVersion>();
            if (indexObj != null)
            {
                JToken versions;
                if (indexObj.TryGetValue(propertyName, out versions))
                {
                    foreach (JToken version in versions)
                    {
                        result.Add(NuGetVersion.Parse(version.ToString()));
                    }
                }
            }
            return result;
        }

        StorageContent CreateContent(HashSet<NuGetVersion> versions, HashSet<NuGetVersion> unlistedVersions)
        {
            JObject obj = new JObject();

            if (versions.Count > 0)
            {
                List<NuGetVersion> result = new List<NuGetVersion>(versions);
                result.Sort();
                obj["versions"] = new JArray(result.Select((v) => v.ToString()));
            }

            if (unlistedVersions.Count > 0)
            {
                List<NuGetVersion> result = new List<NuGetVersion>(unlistedVersions);
                result.Sort();
                obj["unlistedVersions"] = new JArray(result.Select((v) => v.ToString()));
            }

            return new StringStorageContent(obj.ToString(), "application/json");
        }

        static string GetNuspec(Stream stream, string id)
        {
            string name = string.Format("{0}.nuspec", id);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            return null;
        }

        async Task CopyNupkg(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            using (HttpClient client = new HttpClient())
            {
                Uri requestUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));
                using (Stream stream = await client.GetStreamAsync(requestUri))
                {
                    string relativeAddress = string.Format("{1}/{0}.{1}.nupkg", id, version);
                    Uri nupkgUri = new Uri(storage.BaseAddress, string.Format("{1}/{0}.{1}.nupkg", id, version));
                    await storage.Save(nupkgUri, new StreamStorageContent(stream, "application/octet-stream"), cancellationToken);
                }
            }
        }

        bool IsListed(JObject catalogEntry)
        {
            DateTime published = DateTime.Parse(catalogEntry["published"].ToString());
            return published.Year != 1900;
        }
    }
}
