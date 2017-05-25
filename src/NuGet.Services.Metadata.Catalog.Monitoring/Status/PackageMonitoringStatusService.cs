// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Manages the storage and access of the status of packages that validation has run against.
    /// </summary>
    public class PackageMonitoringStatusService : IPackageMonitoringStatusService
    {
        private ILogger<PackageMonitoringStatusService> _logger;

        /// <summary>
        /// The <see cref="StorageFactory"/> to use to save status of packages.
        /// </summary>
        private StorageFactory _storageFactory;

        /// <summary>
        /// The <see cref="JsonSerializerSettings"/> to use to save and load statuses of packages.
        /// </summary>
        private static JsonSerializerSettings SerializerSettings => _serializerSettings.Value;
        private static Lazy<JsonSerializerSettings> _serializerSettings = new Lazy<JsonSerializerSettings>(() =>
        {
            var settings = new JsonSerializerSettings();

            settings.Converters.Add(new NuGetVersionConverter());
            settings.Converters.Add(new StringEnumConverter());

            return settings;
        });

        public PackageMonitoringStatusService(StorageFactory storageFactory, ILogger<PackageMonitoringStatusService> logger)
        {
            _logger = logger;
            _storageFactory = storageFactory;
        }

        public Task<PackageMonitoringStatus> GetAsync(FeedPackageIdentity package, CancellationToken token)
        {
            foreach (var state in Enum.GetNames(typeof(PackageState)))
            {
                var packageStatus = GetPackageAsync(GetStorage(state), package, token);
                if (packageStatus != null)
                {
                    return packageStatus;
                }
            }

            return null;
        }

        public async Task<IEnumerable<PackageMonitoringStatus>> GetAsync(PackageState state, CancellationToken token)
        {
            var packageStatuses = new List<PackageMonitoringStatus>();

            var storage = GetStorage(state);
            foreach (var listItem in await storage.List(token))
            {
                var packageStatus = await GetPackageAsync(storage, listItem.Uri, token);

                if (packageStatus == null)
                {
                    _logger.LogWarning("Unable to get package status from {PackageUri}!", listItem.Uri);
                    continue;
                }

                packageStatuses.Add(packageStatus);
            }

            return packageStatuses;
        }
        
        public async Task UpdateAsync(PackageMonitoringStatus status, CancellationToken token)
        {
            // Guarantee that we never have the same package in multiple states by deleting it first.
            await DeleteAsync(status.Package, token);

            var storage = GetStorage(status.State);
            
            var packageStatusJson = JsonConvert.SerializeObject(status, SerializerSettings);
            var storageContent = new StringStorageContent(packageStatusJson, "application/json");

            var packageUri = GetPackageUri(storage, status.Package);
            await storage.Save(packageUri, storageContent, token);
        }
        
        private Task DeleteAsync(FeedPackageIdentity package, CancellationToken token)
        {
            var tasks = new List<Task>();
            
            foreach (var state in Enum.GetNames(typeof(PackageState)))
            {
                var storage = GetStorage(state);

                var packageUri = GetPackageUri(storage, package);
                tasks.Add(Task.Run(() => storage.Delete(packageUri, token)));
            }

            return Task.WhenAll(tasks);
        }

        private Storage GetStorage(PackageState state)
        {
            return GetStorage(state.ToString());
        }

        private Storage GetStorage(string stateString)
        {
            return _storageFactory.Create(stateString.ToLowerInvariant());
        }

        private Uri GetPackageUri(Storage storage, FeedPackageIdentity package)
        {
            return storage.ResolveUri(GetPackageFileName(package));
        }

        private string GetPackageFileName(FeedPackageIdentity package)
        {
            var idString = package.Id.ToLowerInvariant();
            var versionString = package.Version.ToLowerInvariant();

            return $"{idString}/" +
                $"{idString}.{versionString}.json";
        }

        private Task<PackageMonitoringStatus> GetPackageAsync(Storage storage, FeedPackageIdentity package, CancellationToken token)
        {
            return GetPackageAsync(storage, GetPackageFileName(package), token);
        }

        private Task<PackageMonitoringStatus> GetPackageAsync(Storage storage, string fileName, CancellationToken token)
        {
            if (!storage.Exists(fileName))
            {
                return null;
            }

            return GetPackageAsync(storage, storage.ResolveUri(fileName), token);
        }

        private async Task<PackageMonitoringStatus> GetPackageAsync(Storage storage, Uri packageUri, CancellationToken token)
        {
            return JsonConvert.DeserializeObject<PackageMonitoringStatus>(await GetStorageContentsAsync(storage, packageUri, token), SerializerSettings);
        }

        private async Task<string> GetStorageContentsAsync(Storage storage, Uri uri, CancellationToken token)
        {
            var storageContent = await storage.Load(uri, token);

            var stringStorageContent = storageContent as StringStorageContent;
            if (stringStorageContent != null)
            {
                return stringStorageContent.Content;
            }

            using (var reader = new StreamReader(storageContent.GetContentStream()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
