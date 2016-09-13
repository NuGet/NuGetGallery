// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng
{
    //BUG:  we really want to order-by the LastEdited (in the SortedDictionary) but include the Created in the data (as it is the published date)

    public class Feed2Catalog
    {
        private static readonly DateTime _dateTimeMinValueUtc = new DateTime(0L, DateTimeKind.Utc);
        private readonly ILogger _logger;

        public Feed2Catalog(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Feed2Catalog>();
        }

        public class PackageIdentity
        {
            public PackageIdentity(string id, string version)
            {
                Id = id;
                Version = version;
            }

            public string Id { get; set; }
            public string Version { get; set; }
        }

        public class PackageDetails
        {
            public Uri ContentUri { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime LastEditedDate { get; set; }
            public DateTime PublishedDate { get; set; }
            public string LicenseNames { get; set; }
            public string LicenseReportUrl { get; set; }
        }

        protected virtual HttpClient CreateHttpClient(bool verbose)
        {
            var handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            var handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            return new HttpClient(handler);
        }

        static Uri MakePackageUri(string source, string id, string version)
        {
            var address = string.Format("{0}/Packages?$filter=Id%20eq%20'{1}'%20and%20Version%20eq%20'{2}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                id,
                version);

            return new Uri(address);
        }

        static Uri MakePackageUri(string source, string id)
        {
            var address = string.Format("{0}/Packages?$filter=Id%20eq%20'{1}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                id);

            return new Uri(address);
        }

        static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            var address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            var address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        public static Task<SortedList<DateTime, IList<PackageDetails>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeCreatedUri(source, since, top), "Created");
        }

        public static Task<SortedList<DateTime, IList<PackageDetails>>> GetEditedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeLastEditedUri(source, since, top), "LastEdited");
        }

        private async Task<SortedList<DateTime, IList<PackageIdentity>>> GetDeletedPackages(Storage auditingStorage, HttpClient client, string source, DateTime since, int top = 100)
        {
            var result = new SortedList<DateTime, IList<PackageIdentity>>();

            // Get all audit blobs (based on their filename which starts with a date that can be parsed)
            // NOTE we're getting more files than needed (to account for a time difference between servers)
            var minimumFileTime = since.AddMinutes(-15);
            var auditRecordUris = (await auditingStorage.List(CancellationToken.None))
                .Where(recordUri => FilterDeletedPackage(minimumFileTime, recordUri));

            foreach (var auditRecordUri in auditRecordUris)
            {
                var contents = await auditingStorage.LoadString(auditRecordUri, CancellationToken.None);
                if (!string.IsNullOrEmpty(contents))
                {
                    string packageId;
                    string packageVersion;
                    DateTime? deletedOn;
                    try
                    {
                        var auditRecord = JObject.Parse(contents);

                        var recordPart = (JObject)auditRecord.GetValue("Record", StringComparison.OrdinalIgnoreCase);
                        packageId = recordPart.GetValue("Id", StringComparison.OrdinalIgnoreCase).ToString();
                        packageVersion = recordPart.GetValue("Version", StringComparison.OrdinalIgnoreCase).ToString();

                        var actorPart = (JObject)auditRecord.GetValue("Actor", StringComparison.OrdinalIgnoreCase);
                        deletedOn = actorPart.GetValue("TimestampUtc", StringComparison.OrdinalIgnoreCase).Value<DateTime>();
                    }
                    catch (JsonReaderException)
                    {
                        _logger.LogWarning("Audit record at {AuditRecordUri} contains invalid JSON.", auditRecordUri);
                        continue;
                    }
                    catch (NullReferenceException)
                    {
                        _logger.LogWarning("Audit record at {AuditRecordUri} does not contain required JSON properties to perform a package delete.", auditRecordUri);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion) && deletedOn > since)
                    {
                        // Mark the package "deleted"
                        IList<PackageIdentity> packages;
                        if (!result.TryGetValue(deletedOn.Value, out packages))
                        {
                            packages = new List<PackageIdentity>();
                            result.Add(deletedOn.Value, packages);
                        }

                        packages.Add(new PackageIdentity(packageId, packageVersion));
                    }
                }
            }

            return result;
        }

        private bool FilterDeletedPackage(DateTime minimumFileTime, Uri auditRecordUri)
        {
            var fileName = GetFileName(auditRecordUri);

            // over time, we have had three "deleted" file names. Try working with them all.
            if (fileName.EndsWith("-Deleted.audit.v1.json") || fileName.EndsWith("-deleted.audit.v1.json") || fileName.EndsWith("-softdeleted.audit.v1.json"))
            {
                var deletedDateTimeString = fileName
                    .Replace("-Deleted.audit.v1.json", string.Empty)
                    .Replace("-deleted.audit.v1.json", string.Empty)
                    .Replace("-softdeleted.audit.v1.json", string.Empty)
                    .Replace("_", ":");

                DateTime recordTime;
                if (DateTime.TryParse(deletedDateTimeString, out recordTime))
                {
                    return recordTime >= minimumFileTime;
                }
                else
                {
                    _logger.LogWarning("Could not parse date from filename in FilterDeletedPackage. Uri: {AuditRecordUri}", auditRecordUri);
                }
            }

            return false;
        }

        static string GetFileName(Uri uri)
        {
            var parts = uri.PathAndQuery.Split('/');

            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }

            return null;
        }

        static DateTime ForceUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }
            return date;
        }

        public static async Task<SortedList<DateTime, IList<PackageDetails>>> GetPackages(HttpClient client, Uri uri, string keyDateProperty)
        {
            const string CreatedDateProperty = "Created";
            const string LastEditedDateProperty = "LastEdited";
            const string PublishedDateProperty = "Published";
            const string LicenseNamesProperty = "LicenseNames";
            const string LicenseReportUrlProperty = "LicenseReportUrl";

            var result = new SortedList<DateTime, IList<PackageDetails>>();

            XElement feed;
            using (var stream = await client.GetStreamAsync(uri))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (var entry in feed.Elements(atom + "entry"))
            {
                var content = new Uri(entry.Element(atom + "content").Attribute("src").Value);

                var propertiesElement = entry.Element(metadata + "properties");

                var createdElement = propertiesElement.Element(dataservices + CreatedDateProperty);
                var createdValue = createdElement != null ? createdElement.Value : null;
                var createdDate = string.IsNullOrEmpty(createdValue) ? DateTime.MinValue : DateTime.Parse(createdValue);

                var lastEditedValue = propertiesElement.Element(dataservices + LastEditedDateProperty).Value;
                var lastEditedDate = string.IsNullOrEmpty(lastEditedValue) ? DateTime.MinValue : DateTime.Parse(lastEditedValue);

                var publishedValue = propertiesElement.Element(dataservices + PublishedDateProperty).Value;
                var publishedDate = string.IsNullOrEmpty(publishedValue) ? createdDate : DateTime.Parse(publishedValue);

                var keyEntryValue = propertiesElement.Element(dataservices + keyDateProperty).Value;
                var keyDate = String.IsNullOrEmpty(keyEntryValue) ? createdDate : DateTime.Parse(keyEntryValue);

                // License details
                var licenseNamesElement = propertiesElement.Element(dataservices + LicenseNamesProperty);
                var licenseNames = licenseNamesElement != null ? licenseNamesElement.Value : null;

                var licenseReportUrlElement = propertiesElement.Element(dataservices + LicenseReportUrlProperty);
                var licenseReportUrl = licenseReportUrlElement != null ? licenseReportUrlElement.Value : null;

                // NOTE that DateTime returned by the v2 feed does not have Z at the end even though it is in UTC. So, the DateTime kind is unspecified
                // So, forcibly convert it to UTC here
                createdDate = ForceUtc(createdDate);
                lastEditedDate = ForceUtc(lastEditedDate);
                publishedDate = ForceUtc(publishedDate);
                keyDate = ForceUtc(keyDate);

                IList<PackageDetails> packages;
                if (!result.TryGetValue(keyDate, out packages))
                {
                    packages = new List<PackageDetails>();
                    result.Add(keyDate, packages);
                }

                packages.Add(new PackageDetails
                {
                    ContentUri = content,
                    CreatedDate = createdDate,
                    LastEditedDate = lastEditedDate,
                    PublishedDate = publishedDate,
                    LicenseNames = licenseNames,
                    LicenseReportUrl = licenseReportUrl
                });
            }

            return result;
        }

        private async Task<DateTime> DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<PackageDetails>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, bool? createdPackages, CancellationToken cancellationToken)
        {
            var writer = new AppendOnlyCatalogWriter(storage, maxPageSize: 550);

            var lastDate = DetermineLastDate(lastCreated, lastEdited, createdPackages);

            if (packages == null || packages.Count == 0)
            {
                return lastDate;
            }

            foreach (var entry in packages)
            {
                foreach (var packageItem in entry.Value)
                {
                    // When downloading the package binary, add a query string parameter
                    // that corresponds to the operation's timestamp.
                    // This query string will ensure the package is not cached
                    // (e.g. on the CDN) and returns the "latest and greatest" package metadata.
                    var packageUri = Utilities.GetNugetCacheBustingUri(packageItem.ContentUri, entry.Key.ToString("O"));
                    var response = await client.GetAsync(packageUri, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            CatalogItem item = Utils.CreateCatalogItem(
                                packageItem.ContentUri.ToString(),
                                stream,
                                packageItem.CreatedDate,
                                packageItem.LastEditedDate,
                                packageItem.PublishedDate);

                            if (item != null)
                            {
                                writer.Add(item);

                                _logger.LogInformation("Add metadata from: {PackageDetailsContentUri}", packageItem.ContentUri);
                            }
                            else
                            {
                                _logger.LogWarning("Unable to extract metadata from: {PackageDetailsContentUri}", packageItem.ContentUri);
                            }
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            //  the feed is out of sync with the actual package storage - if we don't have the package there is nothing to be done we might as well move onto the next package
                            _logger.LogWarning("Unable to download: {PackageDetailsContentUri}. Http status: {HttpStatusCode}", packageItem.ContentUri, response.StatusCode);
                        }
                        else
                        {
                            //  this should trigger a restart - of this program - and not move the cursor forward
                            _logger.LogError("Unable to download: {PackageDetailsContentUri}. Http status: {HttpStatusCode}", packageItem.ContentUri, response.StatusCode);
                            throw new Exception(string.Format("Unable to download: {0} http status: {1}", packageItem.ContentUri, response.StatusCode));
                        }
                    }
                }

                lastDate = entry.Key;
            }

            if (createdPackages.HasValue)
            {
                lastCreated = createdPackages.Value ? lastDate : lastCreated;
                lastEdited = !createdPackages.Value ? lastDate : lastEdited;
            }

            var commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, new CommitMetadata(lastCreated, lastEdited, lastDeleted));

            await writer.Commit(commitMetadata, cancellationToken);

            _logger.LogInformation("COMMIT metadata to catalog.");

            return lastDate;
        }

        private async Task<DateTime> Deletes2Catalog(SortedList<DateTime, IList<PackageIdentity>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, CancellationToken cancellationToken)
        {
            var writer = new AppendOnlyCatalogWriter(storage, maxPageSize: 550);

            if (packages == null || packages.Count == 0)
            {
                return lastDeleted;
            }

            foreach (var entry in packages)
            {
                foreach (var packageIdentity in entry.Value)
                {
                    var catalogItem = new DeleteCatalogItem(packageIdentity.Id, packageIdentity.Version, entry.Key);
                    writer.Add(catalogItem);

                    _logger.LogInformation("Delete: {PackageId} {PackageVersion}", packageIdentity.Id, packageIdentity.Version);
                }

                lastDeleted = entry.Key;
            }

            var commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, new CommitMetadata(lastCreated, lastEdited, lastDeleted));

            await writer.Commit(commitMetadata, cancellationToken);

            _logger.LogInformation("COMMIT package deletes to catalog.");

            return lastDeleted;
        }

        private static DateTime DetermineLastDate(DateTime lastCreated, DateTime lastEdited, bool? createdPackages)
        {
            if (createdPackages.HasValue)
            {
                if (createdPackages.Value)
                {
                    return lastCreated;
                }
                else
                {
                    return lastEdited;
                }
            }

            return DateTime.MinValue;
        }

        private static async Task<DateTime?> GetCatalogProperty(Storage storage, string propertyName, CancellationToken cancellationToken)
        {
            var json = await storage.LoadString(storage.ResolveUri("index.json"), cancellationToken);

            if (json != null)
            {
                var obj = JObject.Parse(json);

                JToken token;
                if (obj.TryGetValue(propertyName, out token))
                {
                    return token.ToObject<DateTime>().ToUniversalTime();
                }
            }

            return null;
        }

        private async Task Loop(string gallery, StorageFactory catalogStorageFactory, StorageFactory auditingStorageFactory, bool verbose, int interval, DateTime? startDate, CancellationToken cancellationToken)
        {
            var catalogStorage = catalogStorageFactory.Create();
            var auditingStorage = auditingStorageFactory.Create();

            var top = 20;
            var timeout = TimeSpan.FromSeconds(300);

            while (true)
            {
                await ProcessFeed(gallery, catalogStorage, auditingStorage, startDate, timeout, top, verbose, cancellationToken);

                Thread.Sleep(interval * 1000);
            }
        }

        protected async Task ProcessFeed(string gallery, Storage catalogStorage, Storage auditingStorage, DateTime? startDate, TimeSpan timeout, int top, bool verbose, CancellationToken cancellationToken)
        {
            using (var client = CreateHttpClient(verbose))
            {
                client.Timeout = timeout;

                // baseline timestamps
                var lastCreated = await GetCatalogProperty(catalogStorage, "nuget:lastCreated", cancellationToken) ?? (startDate ?? _dateTimeMinValueUtc);
                var lastEdited = await GetCatalogProperty(catalogStorage, "nuget:lastEdited", cancellationToken) ?? lastCreated;
                var lastDeleted = await GetCatalogProperty(catalogStorage, "nuget:lastDeleted", cancellationToken) ?? lastCreated;
                if (lastDeleted == DateTime.MinValue.ToUniversalTime())
                {
                    lastDeleted = lastCreated;
                }

                // fetch and add all DELETED packages
                if (lastDeleted > DateTime.MinValue.ToUniversalTime())
                {
                    SortedList<DateTime, IList<PackageIdentity>> deletedPackages;
                    var previousLastDeleted = DateTime.MinValue;
                    do
                    {
                        // Get deleted packages
                        _logger.LogInformation("CATALOG LastDeleted: {CatalogDeletedTime}", lastDeleted.ToString("O"));

                        deletedPackages = await GetDeletedPackages(auditingStorage, client, gallery, lastDeleted, top);

                        _logger.LogInformation("FEED DeletedPackages: {DeletedPackagesCount}", deletedPackages.Count);

                        // We want to ensure a commit only contains each package once at most.
                        // Therefore we segment by package id + version.
                        var deletedPackagesSegments = SegmentPackageDeletes(deletedPackages);
                        foreach (var deletedPackagesSegment in deletedPackagesSegments)
                        {
                            lastDeleted = await Deletes2Catalog(
                                deletedPackagesSegment, catalogStorage, lastCreated, lastEdited, lastDeleted, cancellationToken);

                            // Wait for one second to ensure the next catalog commit gets a new timestamp
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                        if (previousLastDeleted == lastDeleted)
                        {
                            break;
                        }
                        previousLastDeleted = lastDeleted;
                    }
                    while (deletedPackages.Count > 0);
                }

                //  THEN fetch and add all newly CREATED packages - in order
                SortedList<DateTime, IList<PackageDetails>> createdPackages;
                var previousLastCreated = DateTime.MinValue;
                do
                {
                    _logger.LogInformation("CATALOG LastCreated: {CatalogLastCreatedTime}", lastCreated.ToString("O"));

                    createdPackages = await GetCreatedPackages(client, gallery, lastCreated, top);
                    _logger.LogInformation("FEED CreatedPackages: {CreatedPackagesCount}", createdPackages.Count);

                    lastCreated = await DownloadMetadata2Catalog(
                        client, createdPackages, catalogStorage, lastCreated, lastEdited, lastDeleted, true, cancellationToken);
                    if (previousLastCreated == lastCreated)
                    {
                        break;
                    }
                    previousLastCreated = lastCreated;
                }
                while (createdPackages.Count > 0);

                //  THEN fetch and add all EDITED packages - in order
                SortedList<DateTime, IList<PackageDetails>> editedPackages;
                var previousLastEdited = DateTime.MinValue;
                do
                {
                    _logger.LogInformation("CATALOG LastEdited: {CatalogLastEditedTime}", lastEdited.ToString("O"));

                    editedPackages = await GetEditedPackages(client, gallery, lastEdited, top);

                    _logger.LogInformation("FEED EditedPackages: {EditedPackagesCount}", editedPackages.Count);

                    lastEdited = await DownloadMetadata2Catalog(
                        client, editedPackages, catalogStorage, lastCreated, lastEdited, lastDeleted, false, cancellationToken);
                    if (previousLastEdited == lastEdited)
                    {
                        break;
                    }
                    previousLastEdited = lastEdited;
                }
                while (editedPackages.Count > 0);
            }
        }

        static IEnumerable<SortedList<DateTime, IList<PackageIdentity>>> SegmentPackageDeletes(SortedList<DateTime, IList<PackageIdentity>> packageDeletes)
        {
            var packageIdentityTracker = new HashSet<string>();
            var currentSegment = new SortedList<DateTime, IList<PackageIdentity>>();
            foreach (var entry in packageDeletes)
            {
                if (!currentSegment.ContainsKey(entry.Key))
                {
                    currentSegment.Add(entry.Key, new List<PackageIdentity>());
                }

                var curentSegmentPackages = currentSegment[entry.Key];
                foreach (var packageIdentity in entry.Value)
                {
                    var key = packageIdentity.Id + "|" + packageIdentity.Version;
                    if (packageIdentityTracker.Contains(key))
                    {
                        // Duplicate, return segment
                        yield return currentSegment;

                        // Clear current segment
                        currentSegment.Clear();
                        currentSegment.Add(entry.Key, new List<PackageIdentity>());
                        curentSegmentPackages = currentSegment[entry.Key];
                        packageIdentityTracker.Clear();
                    }

                    // Add to segment
                    curentSegmentPackages.Add(packageIdentity);
                    packageIdentityTracker.Add(key);
                }
            }

            if (currentSegment.Any())
            {
                yield return currentSegment;
            }
        }


        public void Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = CommandHelpers.GetGallery(arguments);
            var verbose = CommandHelpers.GetVerbose(arguments, required: false);
            var interval = CommandHelpers.GetInterval(arguments, defaultInterval: Constants.DefaultInterval);
            var startDate = CommandHelpers.GetStartDate(arguments);

            var catalogStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);

            _logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\" interval: {Interval}", gallery, catalogStorageFactory, interval);

            DateTime? nullableStartDate = null;
            if (startDate != DateTime.MinValue)
            {
                nullableStartDate = startDate;
            }

            Loop(gallery, catalogStorageFactory, auditingStorageFactory, verbose, interval, nullableStartDate, cancellationToken).Wait();
        }
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng feed2catalog "
                + $"-{Constants.Gallery} <v2-feed-address> "
                + $"-{Constants.StorageBaseAddress} <storage-base-address> "
                + $"-{Constants.StorageType} file|azure "
                + $"[-{Constants.StoragePath} <path>]"
                + "|"
                + $"[-{Constants.StorageAccountName} <azure-acc> "
                    + $"-{Constants.StorageKeyValue} <azure-key> "
                    + $"-{Constants.StorageContainer} <azure-container> "
                    + $"-{Constants.StoragePath} <path> "
                    + $"[-{Constants.VaultName} <keyvault-name> "
                        + $"-{Constants.ClientId} <keyvault-client-id> "
                        + $"-{Constants.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                        + $"[-{Constants.ValidateCertificate} true|false]]] "
                + $"-{Constants.StorageTypeAuditing} file|azure "
                + $"[-{Constants.StoragePathAuditing} <path>]"
                + "|"
                + $"[-{Constants.StorageAccountNameAuditing} <azure-acc> "
                    + $"-{Constants.StorageKeyValueAuditing} <azure-key> "
                    + $"-{Constants.StorageContainerAuditing} <azure-container> "
                    + $"-{Constants.StoragePathAuditing} <path>] "
                + $"[-{Constants.Verbose} true|false] "
                + $"[-{Constants.Interval} <seconds>] "
                + $"[-{Constants.StartDate} <DateTime>]");
        }

        public static void PackagePrintUsage()
        {
            Console.WriteLine("Usage: ng package2catalog "
                + $"-{Constants.Gallery} <v2-feed-address> "
                + $"-{Constants.StorageBaseAddress} <storage-base-address> "
                + $"-{Constants.StorageType} file|azure "
                + $"[-{Constants.StoragePath} <path>]"
                + "|"
                + $"[-{Constants.StorageAccountName} <azure-acc> "
                    + $"-{Constants.StorageKeyValue} <azure-key> "
                    + $"-{Constants.StorageContainer} <azure-container> "
                    + $"-{Constants.StoragePath} <path>] "
                + $"[-{Constants.Verbose} true|false] "
                + $"-{Constants.Id} <id> "
                + $"[-{Constants.Version} <version>]");
        }

        public void Package(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = CommandHelpers.GetGallery(arguments);
            var verbose = CommandHelpers.GetVerbose(arguments, required: false);
            var id = CommandHelpers.GetId(arguments);
            var version = CommandHelpers.GetVersion(arguments);
            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            ProcessPackages(gallery, storageFactory, id, version, verbose, cancellationToken).Wait();
        }

        async Task ProcessPackages(string gallery, StorageFactory storageFactory, string id, string version, bool verbose, CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(300);

            using (var client = CreateHttpClient(verbose))
            {
                client.Timeout = timeout;

                //  if the version is specified a single package is processed otherwise all the packages corresponding to that id are processed

                var uri = (version == null) ? MakePackageUri(gallery, id) : MakePackageUri(gallery, id, version);

                var packages = await GetPackages(client, uri, "Created");

                _logger.LogInformation(string.Format("Downloading {0} packages", packages.Select(t => t.Value.Count).Sum()));

                var storage = storageFactory.Create();

                //  the idea here is to leave the lastCreated, lastEdited and lastDeleted values exactly as they were
                var lastCreated = await GetCatalogProperty(storage, "nuget:lastCreated", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                var lastEdited = await GetCatalogProperty(storage, "nuget:lastEdited", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                var lastDeleted = await GetCatalogProperty(storage, "nuget:lastDeleted", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();

                await DownloadMetadata2Catalog(client, packages, storage, lastCreated, lastEdited, lastDeleted, null, cancellationToken);
            }
        }
    }
}
