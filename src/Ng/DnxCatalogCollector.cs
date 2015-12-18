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
                string type = item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet);

                Storage storage = _storageFactory.Create(id);

                if (type == Schema.DataTypes.PackageDetails.ToString())
                {
                    // Add/update package
                    string nuspec = await LoadNuspec(client, id, version, cancellationToken);
                    if (nuspec != null)
                    {
                        await SaveNuspec(storage, id, version, nuspec, cancellationToken);
                        await CopyNupkg(client, storage, id, version, cancellationToken);
                        await UpdateMetadata(storage, versions => versions.Add(NuGetVersion.Parse(version)), cancellationToken);

                        Trace.TraceInformation("commit: {0}/{1}", id, version);
                    }
                    else
                    {
                        Trace.TraceWarning("no nuspec available for {0}/{1} skipping", id, version);
                    }
                }
                else if (type == Schema.DataTypes.PackageDelete.ToString())
                {
                    // Delete package
                    await UpdateMetadata(storage, versions => versions.Remove(NuGetVersion.Parse(version)), cancellationToken);
                    await DeleteNuspec(storage, id, version, cancellationToken);
                    await DeleteNupkg(storage, id, version, cancellationToken);

                    Trace.TraceInformation("commit delete: {0}/{1}", id, version);
                }
            }

            return true;
        }

        async Task UpdateMetadata(Storage storage, Action<HashSet<NuGetVersion>> updateAction, CancellationToken cancellationToken)
        {
            string relativeAddress = "index.json";

            Uri resourceUri = new Uri(storage.BaseAddress, relativeAddress);
            HashSet<NuGetVersion> versions = GetVersions(await storage.LoadString(resourceUri, cancellationToken));
            updateAction(versions);
            List<NuGetVersion> result = new List<NuGetVersion>(versions);

            if (result.Any())
            {
                // Store versions (sorted)
                result.Sort();
                await storage.Save(resourceUri, CreateContent(result.Select((v) => v.ToString())), cancellationToken);
            }
            else
            {
                // Remove versions file if no versions are present
                if (storage.Exists(relativeAddress))
                {
                    await storage.Delete(resourceUri, cancellationToken);
                }
            }
        }

        private async Task<string> LoadNuspec(HttpClient client, string id, string version, CancellationToken cancellationToken)
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

        private async Task SaveNuspec(Storage storage, string id, string version, string nuspec, CancellationToken cancellationToken)
        {
            string relativeAddress = string.Format("{1}/{0}.nuspec", id, version);
            Uri nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            await storage.Save(nuspecUri, new StringStorageContent(nuspec, "text/xml", "max-age=120"), cancellationToken);
        }

        private async Task DeleteNuspec(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            string relativeAddress = string.Format("{1}/{0}.nuspec", id, version);
            Uri nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            if (storage.Exists(relativeAddress))
            {
                await storage.Delete(nuspecUri, cancellationToken);
            }
        }

        private static HashSet<NuGetVersion> GetVersions(string json)
        {
            HashSet<NuGetVersion> result = new HashSet<NuGetVersion>();
            if (json != null)
            {
                JObject obj = JObject.Parse(json);

                JArray versions = obj["versions"] as JArray;

                if (versions != null)
                {
                    foreach (JToken version in versions)
                    {
                        result.Add(NuGetVersion.Parse(version.ToString()));
                    }
                }
            }
            return result;
        }

        private StorageContent CreateContent(IEnumerable<string> versions)
        {
            JObject obj = new JObject { { "versions", new JArray(versions) } };
            return new StringStorageContent(obj.ToString(), "application/json", "no-store");
        }

        private static string GetNuspec(Stream stream, string id)
        {
            string name = string.Format("{0}.nuspec", id);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                // first look for a nuspec file named as the package id
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
                // failing that, just return the first file that appears to be a nuspec
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase))
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

        private async Task CopyNupkg(HttpClient client, Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            Uri requestUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));
            using (Stream stream = await client.GetStreamAsync(requestUri))
            {
                string relativeAddress = string.Format("{1}/{0}.{1}.nupkg", id, version);
                Uri nupkgUri = new Uri(storage.BaseAddress, string.Format("{1}/{0}.{1}.nupkg", id, version));
                await storage.Save(nupkgUri, new StreamStorageContent(stream, "application/octet-stream", "max-age=120"), cancellationToken);
            }
        }

        private async Task DeleteNupkg(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            string relativeAddress = string.Format("{1}/{0}.{1}.nupkg", id, version);
            Uri nupkgUri = new Uri(storage.BaseAddress, relativeAddress);
            if (storage.Exists(relativeAddress))
            {
                await storage.Delete(nupkgUri, cancellationToken);
            }
        }
    }
}
