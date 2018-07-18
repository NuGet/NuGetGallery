// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxMaker
    {
        private readonly StorageFactory _storageFactory;

        public DnxMaker(StorageFactory storageFactory)
        {
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
        }

        public async Task<DnxEntry> AddPackageAsync(
            Stream nupkgStream,
            string nuspec,
            string id,
            string version,
            CancellationToken cancellationToken)
        {
            if (nupkgStream == null)
            {
                throw new ArgumentNullException(nameof(nupkgStream));
            }

            if (string.IsNullOrEmpty(nuspec))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(nuspec));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(version));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var storage = _storageFactory.Create(id);
            var normalizedVersion = NuGetVersionUtility.NormalizeVersion(version);

            var nuspecUri = await SaveNuspecAsync(storage, id, normalizedVersion, nuspec, cancellationToken);
            var nupkgUri = await SaveNupkgAsync(nupkgStream, storage, id, normalizedVersion, cancellationToken);

            return new DnxEntry(nupkgUri, nuspecUri);
        }

        public async Task DeletePackageAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(version));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var storage = _storageFactory.Create(id);
            var normalizedVersion = NuGetVersionUtility.NormalizeVersion(version);

            await DeleteNuspecAsync(storage, id, normalizedVersion, cancellationToken);
            await DeleteNupkgAsync(storage, id, normalizedVersion, cancellationToken);
        }

        public async Task<bool> HasPackageInIndexAsync(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(version));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var versionsContext = await GetVersionsAsync(storage, cancellationToken);
            var parsedVersion = NuGetVersion.Parse(version);

            return versionsContext.Versions.Contains(parsedVersion);
        }

        private async Task<Uri> SaveNuspecAsync(Storage storage, string id, string version, string nuspec, CancellationToken cancellationToken)
        {
            var relativeAddress = GetRelativeAddressNuspec(id, version);
            var nuspecUri = new Uri(storage.BaseAddress, relativeAddress);

            await storage.Save(nuspecUri, new StringStorageContent(nuspec, "text/xml", "max-age=120"), cancellationToken);

            return nuspecUri;
        }

        public async Task UpdatePackageVersionIndexAsync(string id, Action<HashSet<NuGetVersion>> updateAction, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var storage = _storageFactory.Create(id);
            var versionsContext = await GetVersionsAsync(storage, cancellationToken);
            var relativeAddress = versionsContext.RelativeAddress;
            var resourceUri = versionsContext.ResourceUri;
            var versions = versionsContext.Versions;

            updateAction(versions);
            List<NuGetVersion> result = new List<NuGetVersion>(versions);

            if (result.Any())
            {
                // Store versions (sorted)
                result.Sort();

                await storage.Save(resourceUri, CreateContent(result.Select(version => version.ToNormalizedString())), cancellationToken);
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

        private async Task<VersionsResult> GetVersionsAsync(Storage storage, CancellationToken cancellationToken)
        {
            var relativeAddress = "index.json";
            var resourceUri = new Uri(storage.BaseAddress, relativeAddress);
            var versions = GetVersions(await storage.LoadString(resourceUri, cancellationToken));

            return new VersionsResult(relativeAddress, resourceUri, versions);
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

        private async Task<Uri> SaveNupkgAsync(Stream nupkgStream, Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            Uri nupkgUri = new Uri(storage.BaseAddress, GetRelativeAddressNupkg(id, version));
            await storage.Save(nupkgUri, new StreamStorageContent(nupkgStream, "application/octet-stream", "max-age=120"), cancellationToken);
            return nupkgUri;
        }

        private async Task DeleteNuspecAsync(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            string relativeAddress = GetRelativeAddressNuspec(id, version);
            Uri nuspecUri = new Uri(storage.BaseAddress, relativeAddress);
            if (storage.Exists(relativeAddress))
            {
                await storage.Delete(nuspecUri, cancellationToken);
            }
        }

        private async Task DeleteNupkgAsync(Storage storage, string id, string version, CancellationToken cancellationToken)
        {
            string relativeAddress = GetRelativeAddressNupkg(id, version);
            Uri nupkgUri = new Uri(storage.BaseAddress, relativeAddress);
            if (storage.Exists(relativeAddress))
            {
                await storage.Delete(nupkgUri, cancellationToken);
            }
        }

        private static string GetRelativeAddressNuspec(string id, string version)
        {
            return $"{NuGetVersion.Parse(version).ToNormalizedString()}/{id}.nuspec";
        }

        public static string GetRelativeAddressNupkg(string id, string version)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(version));
            }

            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            return $"{normalizedVersion}/{id}.{normalizedVersion}.nupkg";
        }

        private class VersionsResult
        {
            public VersionsResult(string relativeAddress, Uri resourceUri, HashSet<NuGetVersion> versions)
            {
                RelativeAddress = relativeAddress;
                ResourceUri = resourceUri;
                Versions = versions;
            }

            public string RelativeAddress { get; }
            public Uri ResourceUri { get; }
            public HashSet<NuGetVersion> Versions { get; }
        }
    }
}