// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

using CatalogStorage = NuGet.Services.Metadata.Catalog.Persistence.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Manages the storage and access of the status of packages that validation has run against.
    /// </summary>
    public class PackageMonitoringStatusService : IPackageMonitoringStatusService
    {
        private static string[] _packageStateNames = Enum.GetNames(typeof(PackageState));
        private static Array _packageStateValues = Enum.GetValues(typeof(PackageState));

        private ILogger<PackageMonitoringStatusService> _logger;

        /// <summary>
        /// The <see cref="IStorageFactory"/> to use to save status of packages.
        /// </summary>
        private IStorageFactory _storageFactory;

        public PackageMonitoringStatusService(IStorageFactory storageFactory, ILogger<PackageMonitoringStatusService> logger)
        {
            _logger = logger;
            _storageFactory = storageFactory;
        }

        public async Task<IEnumerable<PackageMonitoringStatusListItem>> ListAsync(CancellationToken token)
        {
            var listTasks =
                _packageStateNames
                .Select(state => GetListItems(state, token))
                .ToList();

            return
                (await Task.WhenAll(listTasks))
                .Where(list => list != null && list.Any())
                .Aggregate((current, next) => current.Concat(next))
                .Where(i => i != null);
        }

        private async Task<IEnumerable<PackageMonitoringStatusListItem>> GetListItems(string state, CancellationToken token)
        {
            var storage = GetStorage(state);
            var list = await storage.ListAsync(token);
            return list.Select(item =>
            {
                try
                {
                    return new PackageMonitoringStatusListItem(ParsePackageUri(item.Uri), (PackageState)Enum.Parse(typeof(PackageState), state));
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Failed to parse list item {ItemUri}: {Exception}", item.Uri, e);
                    return null;
                }
            });
        }

        public async Task<PackageMonitoringStatus> GetAsync(FeedPackageIdentity package, CancellationToken token)
        {
            var statusTasks =
                _packageStateNames
                .Select(state => GetPackageAsync(GetStorage(state), package, token))
                .ToList();

            var statuses =
                (await Task.WhenAll(statusTasks))
                .Where(s => s != null);

            if (!statuses.Any())
            {
                return null;
            }

            // If more than one status exist for a single package, find the status with the latest timestamp.
            var statusesWithTimeStamps = statuses.Where(s => s.ValidationResult != null && s.ValidationResult.CatalogEntries != null && s.ValidationResult.CatalogEntries.Any());
            if (statusesWithTimeStamps.Any())
            {
                return statusesWithTimeStamps.OrderByDescending(s => s.ValidationResult.CatalogEntries.Max(c => c.CommitTimeStamp)).First();
            }
            else
            {
                // No statuses have timestamps (they all failed to process).
                // Because they are all in a bad state, choose an arbitrary one.
                return statuses.First();
            }
        }

        public async Task<IEnumerable<PackageMonitoringStatus>> GetAsync(PackageState state, CancellationToken token)
        {
            var packageStatuses = new List<PackageMonitoringStatus>();

            var storage = GetStorage(state);

            var statusTasks =
                (await storage.ListAsync(token))
                .Select(listItem => GetPackageAsync(storage, listItem.Uri, token))
                .ToList();

            return
                (await Task.WhenAll(statusTasks))
                .Where(s => s != null);
        }

        public async Task UpdateAsync(PackageMonitoringStatus status, CancellationToken token)
        {
            // Save the new status first.
            // If we save it after deleting the existing status, the save could fail and then we'd lose the data.
            await SaveAsync(status, token);

            foreach (int stateInt in _packageStateValues)
            {
                var state = (PackageState)stateInt;
                if (state != status.State)
                {
                    // Delete the existing status.
                    await DeleteAsync(status.Package, state, token);
                }
            }
        }

        private async Task SaveAsync(PackageMonitoringStatus status, CancellationToken token)
        {
            var storage = GetStorage(status.State);

            var packageStatusJson = JsonConvert.SerializeObject(status, JsonSerializerUtility.SerializerSettings);
            var storageContent = new StringStorageContent(packageStatusJson, "application/json");

            var packageUri = GetPackageUri(storage, status.Package);

            await storage.SaveAsync(packageUri, storageContent, token);
        }

        private async Task DeleteAsync(FeedPackageIdentity package, PackageState state, CancellationToken token)
        {
            var storage = GetStorage(state);
            if (!storage.Exists(GetPackageFileName(package)))
            {
                return;
            }

            await storage.DeleteAsync(GetPackageUri(storage, package), token);
        }

        private CatalogStorage GetStorage(PackageState state)
        {
            return GetStorage(state.ToString());
        }

        private CatalogStorage GetStorage(string stateString)
        {
            return _storageFactory.Create(stateString.ToLowerInvariant());
        }

        private Uri GetPackageUri(CatalogStorage storage, FeedPackageIdentity package)
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

        /// <summary>
        /// Parses a <see cref="Uri"/> into a <see cref="FeedPackageIdentity"/>.
        /// 
        /// The <see cref="Uri"/> must end with "/{id}/{id}.{version}.json"
        /// </summary>
        private FeedPackageIdentity ParsePackageUri(Uri packageUri)
        {
            var uriSegments = packageUri.Segments;
            // The second to last segment is the id.
            var id = uriSegments[uriSegments.Length - 2].Trim('/');

            // The last segment is {id}.{version}.json.
            // Remove the id and the "." from the beginning.
            var version = uriSegments[uriSegments.Length - 1].Substring(id.Length + ".".Length);
            // Remove the ".json" from the end.
            version = version.Substring(0, version.Length - ".json".Length);

            return new FeedPackageIdentity(id, version);
        }

        private Task<PackageMonitoringStatus> GetPackageAsync(CatalogStorage storage, FeedPackageIdentity package, CancellationToken token)
        {
            return GetPackageAsync(storage, GetPackageFileName(package), token);
        }

        private Task<PackageMonitoringStatus> GetPackageAsync(CatalogStorage storage, string fileName, CancellationToken token)
        {
            if (!storage.Exists(fileName))
            {
                return Task.FromResult<PackageMonitoringStatus>(null);
            }

            return GetPackageAsync(storage, storage.ResolveUri(fileName), token);
        }

        private async Task<PackageMonitoringStatus> GetPackageAsync(CatalogStorage storage, Uri packageUri, CancellationToken token)
        {
            try
            {
                var content = await storage.LoadAsync(packageUri, token);
                string statusString = null;
                using (var stream = content.GetContentStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        statusString = await reader.ReadToEndAsync();
                    }
                }

                var status = JsonConvert.DeserializeObject<PackageMonitoringStatus>(statusString, JsonSerializerUtility.SerializerSettings);

                return status;
            }
            catch (Exception deserializationException)
            {
                _logger.LogWarning(
                    LogEvents.StatusDeserializationFailure,
                    deserializationException,
                    "Unable to deserialize package status from {PackageUri}!",
                    packageUri);

                try
                {
                    /// Construct a <see cref="PackageMonitoringStatus"/> from the <see cref="Uri"/> with this as the exception.
                    return new PackageMonitoringStatus(ParsePackageUri(packageUri), new StatusDeserializationException(deserializationException));
                }
                catch (Exception uriParsingException)
                {
                    _logger.LogError(
                        LogEvents.StatusDeserializationFatalFailure,
                        new AggregateException(deserializationException, uriParsingException),
                        "Unable to get package id and version from {PackageUri}!",
                        packageUri);

                    return null;
                }
            }
        }
    }
}