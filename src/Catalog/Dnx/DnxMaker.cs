// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxMaker
    {
        private readonly StorageFactory _storageFactory;

        public class DnxEntry
        {
            public Uri Nupkg { get; set; }
            public Uri Nuspec { get; set; }
        }

        public DnxMaker(StorageFactory storageFactory)
        {
            if (storageFactory == null)
            {
                throw new ArgumentNullException(nameof(storageFactory));
            }

            _storageFactory = storageFactory;
        }

        public async Task<DnxEntry> AddPackage(Stream nupkgStream, string nuspec, string id, string version, CancellationToken cancellationToken)
        {
            Storage storage = _storageFactory.Create(id);

            var nuspecUri = await SaveNuspec(storage, id, version, nuspec, cancellationToken);
            var nupkgUri = await SaveNupkg(nupkgStream, storage, id, version, cancellationToken);
            await UpdateMetadata(storage, versions => versions.Add(NuGetVersion.Parse(version)), cancellationToken);

            return new DnxEntry { Nupkg = nupkgUri, Nuspec = nuspecUri };
        }

        public async Task DeletePackage(string id, string version, CancellationToken cancellationToken)
        {
            Storage storage = _storageFactory.Create(id);

            await UpdateMetadata(storage, versions => versions.Remove(NuGetVersion.Parse(version)), cancellationToken);
            await DeleteNuspec(storage, id, version, cancellationToken);
            await DeleteNupkg(storage, id, version, cancellationToken);
        }

        private async Task<Uri> SaveNuspec(Storage storage, string id, string version, string nuspec, CancellationToken cancellationToken)
        {
            var relativeAddress = string.Format("{1}/{0}.nuspec", id, version);
            var nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            await storage.Save(nuspecUri, new StringStorageContent(nuspec, "text/xml", "max-age=120"), cancellationToken);
            return nuspecUri;
        }

        async Task UpdateMetadata(Storage storage, Action<HashSet<NuGetVersion>> updateAction, CancellationToken cancellationToken)
        {
            string relativeAddress = "index.json";
            var resourceUri = new Uri(storage.BaseAddress, relativeAddress);
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

        private static HashSet<NuGetVersion> GetVersions(string json)
        {
            var result = new HashSet<NuGetVersion>();
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

        private async Task<Uri> SaveNupkg(Stream nupkgStream, Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            Uri nupkgUri = new Uri(storage.BaseAddress, string.Format("{1}/{0}.{1}.nupkg", id, version));
            await storage.Save(nupkgUri, new StreamStorageContent(nupkgStream, "application/octet-stream", "max-age=120"), cancellationToken);
            return nupkgUri;
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
