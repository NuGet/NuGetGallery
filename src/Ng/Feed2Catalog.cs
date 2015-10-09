// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using VDS.RDF;

namespace Ng
{
    //BUG:  we really want to order-by the LastEdited (in the SortedDictionary) but include the Created in the data (as it is the published date)

    public static class Feed2Catalog
    {
        #region private_strings
        private const string CreatedDateProperty = "Created";
        private const string LastEditedDateProperty = "LastEdited";
        private const string PublishedDateProperty = "Published";
        private const string LicenseNamesProperty = "LicenseNames";
        private const string LicenseReportUrlProperty = "LicenseReportUrl";
        #endregion

        public class FeedDetails
        {
            public DateTime CreatedDate { get; set; }
            public DateTime LastEditedDate { get; set; }
            public DateTime PublishedDate { get; set; }
            public string LicenseNames { get; set; }
            public string LicenseReportUrl { get; set; }
        }

        static Uri MakePackageUri(string source, string id, string version)
        {
            string address = string.Format("{0}/Packages?$filter=Id%20eq%20'{1}'%20and%20Version%20eq%20'{2}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                id,
                version);

            return new Uri(address);
        }

        static Uri MakePackageUri(string source, string id)
        {
            string address = string.Format("{0}/Packages?$filter=Id%20eq%20'{1}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                id);

            return new Uri(address);
        }

        static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            string address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        public static Task<SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeCreatedUri(source, since, top), "Created");
        }

        public static Task<SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>>> GetEditedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return GetPackages(client, MakeLastEditedUri(source, since, top), "LastEdited");
        }

        public static async Task<SortedList<DateTime, IList<Tuple<string, string>>>> GetDeletedPackages(Storage auditingStorage, HttpClient client, string source, DateTime since, int top = 100)
        {
            SortedList<DateTime, IList<Tuple<string, string>>> result = new SortedList<DateTime, IList<Tuple<string, string>>>();

            // Get all audit blobs (based on their filename which startswith a date that can be parsed)
            var auditRecordUris = (await auditingStorage.List(true, CancellationToken.None))
                .Where(recordUri => FilterDeletedPackage(since, recordUri));
            
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
                        Trace.TraceWarning(string.Format("Audit record at {0} contains invalid JSON.", auditRecordUri));
                        continue;
                    }
                    catch (NullReferenceException)
                    {
                        Trace.TraceWarning(string.Format("Audit record at {0} does not contain required JSON properties to perform a package delete.", auditRecordUri));
                        continue;
                    }

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion)  
                        && deletedOn.HasValue && deletedOn >= since)
                    {
                        // Check if the package exists in the feed. This prevents out-of-order processing
                        // from deleting the package if it's already uploaded again.
                        if (!(await PackageExists(client, source, packageId, packageVersion)))
                        {
                            // Mark the package "deleted"
                            IList<Tuple<string, string>> packages;
                            if (!result.TryGetValue(deletedOn.Value, out packages))
                            {
                                packages = new List<Tuple<string, string>>();
                                result.Add(deletedOn.Value, packages);
                            }

                            packages.Add(new Tuple<string, string>(packageId, packageVersion));
                        }
                    }
                }
            }

            return result;
        }

        private static bool FilterDeletedPackage(DateTime since, Uri recordUri)
        {
            string fileName = GetFileName(recordUri);

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
                    return recordTime >= since;
                }
                else
                {
                    Trace.TraceWarning(string.Format("Could not parse date from filename in FilterDeletedPackage. Uri: {0}", recordUri));
                }
            }

            return false;
        }

        private static string GetFileName(Uri uri)
        {
            string[] parts = uri.PathAndQuery.Split('/');

            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }

            return null;
        }

        private static DateTime ForceUTC(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }
            return date;
        }

        public static async Task<SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>>> GetPackages(HttpClient client, Uri uri, string keyDateProperty)
        {
            SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>> result = new SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>>();

            XElement feed;
            using (Stream stream = await client.GetStreamAsync(uri))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (XElement entry in feed.Elements(atom + "entry"))
            {
                Uri content = new Uri(entry.Element(atom + "content").Attribute("src").Value);

                XElement propertiesElement = entry.Element(metadata + "properties");

                XElement createdElement = propertiesElement.Element(dataservices + CreatedDateProperty);
                string createdValue = createdElement != null ? createdElement.Value : null;
                DateTime createdDate = String.IsNullOrEmpty(createdValue) ? DateTime.MinValue : DateTime.Parse(createdValue);

                XElement lastEditedElement = propertiesElement.Element(dataservices + LastEditedDateProperty);
                string lastEditedValue = propertiesElement.Element(dataservices + LastEditedDateProperty).Value;
                DateTime lastEditedDate = String.IsNullOrEmpty(lastEditedValue) ? DateTime.MinValue : DateTime.Parse(lastEditedValue);

                XElement publishedElement = propertiesElement.Element(dataservices + PublishedDateProperty);
                string publishedValue = propertiesElement.Element(dataservices + PublishedDateProperty).Value;
                DateTime publishedDate = String.IsNullOrEmpty(publishedValue) ? createdDate : DateTime.Parse(publishedValue);

                XElement keyElement = propertiesElement.Element(dataservices + keyDateProperty);
                string keyEntryValue = propertiesElement.Element(dataservices + keyDateProperty).Value;
                DateTime keyDate = String.IsNullOrEmpty(keyEntryValue) ? createdDate : DateTime.Parse(keyEntryValue);

                // License details
                XElement licenseNamesElement = propertiesElement.Element(dataservices + LicenseNamesProperty);
                string licenseNames = licenseNamesElement != null ? licenseNamesElement.Value : null;

                XElement licenseReportUrlElement = propertiesElement.Element(dataservices + LicenseReportUrlProperty);
                string licenseReportUrl = licenseReportUrlElement != null ? licenseReportUrlElement.Value : null;

                // NOTE that DateTime returned by the v2 feed does not have Z at the end even though it is in UTC. So, the DateTime kind is unspecified
                // So, forcibly convert it to UTC here
                createdDate = ForceUTC(createdDate);
                lastEditedDate = ForceUTC(lastEditedDate);
                publishedDate = ForceUTC(publishedDate);
                keyDate = ForceUTC(keyDate);

                IList<Tuple<Uri, FeedDetails>> contentUris;
                if (!result.TryGetValue(keyDate, out contentUris))
                {
                    contentUris = new List<Tuple<Uri, FeedDetails>>();
                    result.Add(keyDate, contentUris);
                }

                FeedDetails details = new FeedDetails
                {
                    CreatedDate = createdDate,
                    LastEditedDate = lastEditedDate,
                    PublishedDate = publishedDate,
                    LicenseNames = licenseNames,
                    LicenseReportUrl = licenseReportUrl
                };

                contentUris.Add(new Tuple<Uri, FeedDetails>(content, details));
            }

            return result;
        }

        public static async Task<bool> PackageExists(HttpClient client, string source, string id, string version)
        {
            XElement feed;
            using (Stream stream = await client.GetStreamAsync(MakePackageUri(source, id, version)))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";

            return feed.Elements(atom + "entry").Any();
        }

        static async Task<DateTime> DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, bool? createdPackages, CancellationToken cancellationToken)
        {
            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            DateTime lastDate = createdPackages.HasValue ? (createdPackages.Value ? lastCreated : lastEdited) : DateTime.MinValue;

            if (packages == null || packages.Count == 0)
            {
                return lastDate;
            }

            foreach (KeyValuePair<DateTime, IList<Tuple<Uri, FeedDetails>>> entry in packages)
            {
                foreach (Tuple<Uri, FeedDetails> packageItem in entry.Value)
                {
                    Uri uri = packageItem.Item1;
                    FeedDetails details = packageItem.Item2;

                    HttpResponseMessage response = await client.GetAsync(uri,cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            CatalogItem item = Utils.CreateCatalogItem(stream, entry.Key, null, uri.ToString(), details.CreatedDate, details.LastEditedDate, details.PublishedDate, details.LicenseNames, details.LicenseReportUrl);

                            if (item != null)
                            {
                                writer.Add(item);

                                Trace.TraceInformation("Add: {0}", uri);
                            }
                            else
                            {
                                Trace.TraceWarning("Unable to extract metadata from: {0}", uri);
                            }
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            //  the feed is out of sync with the actual package storage - if we don't have the package there is nothing to be done we might as well move onto the next package
                            Trace.TraceWarning(string.Format("Unable to download: {0} http status: {1}", uri, response.StatusCode));
                        }
                        else
                        {
                            //  this should trigger a restart - of this program - and not more the cursor forward
                            Trace.TraceError(string.Format("Unable to download: {0} http status: {1}", uri, response.StatusCode));
                            throw new Exception(string.Format("Unable to download: {0} http status: {1}", uri, response.StatusCode));
                        }
                    }
                }

                lastDate = entry.Key;
            }

            if (createdPackages.HasValue)
            {
                lastCreated = createdPackages.Value ? lastDate : lastCreated;
                lastEdited = !createdPackages.Value ? lastDate : lastEdited;
                lastDeleted = !createdPackages.Value ? lastDate : lastDeleted;
            }

            IGraph commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, lastEdited, lastDeleted);
            
            await writer.Commit(commitMetadata, cancellationToken);

            Trace.TraceInformation("COMMIT");

            return lastDate;
        }

        static async Task<DateTime> Deletes2Catalog(SortedList<DateTime, IList<Tuple<string, string>>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, bool? createdPackages, CancellationToken cancellationToken)
        {
            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            DateTime lastDate = createdPackages.HasValue ? (createdPackages.Value ? lastCreated : lastEdited) : DateTime.MinValue;

            if (packages == null || packages.Count == 0)
            {
                return lastDate;
            }

            foreach (KeyValuePair<DateTime, IList<Tuple<string, string>>> entry in packages)
            {
                foreach (Tuple<string, string> packageItem in entry.Value)
                {
                    string id = packageItem.Item1;
                    string version = packageItem.Item2;

                    var catalogItem = new DeleteCatalogItem(id, version, entry.Key);
                    writer.Add(catalogItem);

                    Trace.TraceInformation("Delete: {0} {1}", id, version);
                }

                lastDate = entry.Key;
            }

            if (createdPackages.HasValue)
            {
                lastCreated = createdPackages.Value ? lastDate : lastCreated;
                lastEdited = !createdPackages.Value ? lastDate : lastEdited;
                lastDeleted = !createdPackages.Value ? lastDate : lastDeleted;
            }

            IGraph commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, lastEdited, lastDeleted);

            await writer.Commit(commitMetadata, cancellationToken);

            Trace.TraceInformation("COMMIT");

            return lastDate;
        }

        static async Task<DateTime?> GetCatalogProperty(Storage storage, string propertyName, CancellationToken cancellationToken)
        {
            string json = await storage.LoadString(storage.ResolveUri("index.json"), cancellationToken);

            if (json != null)
            {
                JObject obj = JObject.Parse(json);

                JToken token;
                if (obj.TryGetValue(propertyName, out token))
                {
                    return token.ToObject<DateTime>().ToUniversalTime();
                }
            }

            return null;
        }

        static async Task Loop(string gallery, StorageFactory catalogStorageFactory, StorageFactory auditingStorageFactory, bool verbose, int interval, DateTime? startDate, CancellationToken cancellationToken)
        {
            Storage catalogStorage = catalogStorageFactory.Create();
            Storage auditingStorage = auditingStorageFactory.Create();

            const string LastCreated = "nuget:lastCreated";
            const string LastEdited = "nuget:lastEdited";
            const string LastDeleted = "nuget:lastDeleted";

            int top = 20;
            int timeout = 300;

            while (true)
            {
                Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

                HttpMessageHandler handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(timeout);

                    //  fetch and add all newly CREATED packages - in order
                    DateTime lastCreated = await GetCatalogProperty(catalogStorage, LastCreated, cancellationToken) ?? (startDate ?? DateTime.MinValue.ToUniversalTime());
                    DateTime lastEdited = await GetCatalogProperty(catalogStorage, LastEdited, cancellationToken) ?? lastCreated;
                    DateTime lastDeleted = await GetCatalogProperty(catalogStorage, LastDeleted, cancellationToken) ?? lastCreated;
                    if (lastDeleted == DateTime.MinValue.ToUniversalTime())
                    {
                        lastDeleted = lastCreated;
                    }

                    SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>> createdPackages;
                    DateTime previousLastCreated = DateTime.MinValue;
                    do
                    {
                        Trace.TraceInformation("CATALOG LastCreated: {0}", lastCreated.ToString("O"));

                        createdPackages = await GetCreatedPackages(client, gallery, lastCreated, top);
                        Trace.TraceInformation("FEED CreatedPackages: {0}", createdPackages.Count);

                        lastCreated = await DownloadMetadata2Catalog(client, createdPackages, catalogStorage, lastCreated, lastEdited, lastDeleted, true, cancellationToken);
                        if (previousLastCreated == lastCreated)
                        {
                            break;
                        }
                        previousLastCreated = lastCreated;
                    }
                    while (createdPackages.Count > 0);

                    //  THEN fetch and add all EDITED packages - in order
                    SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>> editedPackages;
                    DateTime previousLastEdited = DateTime.MinValue;
                    do
                    {
                        Trace.TraceInformation("CATALOG LastEdited: {0}", lastEdited.ToString("O"));

                        editedPackages = await GetEditedPackages(client, gallery, lastEdited, top);

                        Trace.TraceInformation("FEED EditedPackages: {0}", editedPackages.Count);

                        lastEdited = await DownloadMetadata2Catalog(client, editedPackages, catalogStorage, lastCreated, lastEdited, lastDeleted, false, cancellationToken);
                        if (previousLastEdited == lastEdited)
                        {
                            break;
                        }
                        previousLastEdited = lastEdited;
                    }
                    while (editedPackages.Count > 0);

                    // THEN fetch and add all DELETED packages - the GetDeletedPackages verifies if the delete is to be processed
                    if (lastDeleted > DateTime.MinValue.ToUniversalTime())
                    {
                        SortedList<DateTime, IList<Tuple<string, string>>> deletedPackages;
                        DateTime previousLastDeleted = DateTime.MinValue;
                        do
                        {
                            Trace.TraceInformation("CATALOG LastDeleted: {0}", lastDeleted.ToString("O"));

                            deletedPackages = await GetDeletedPackages(auditingStorage, client, gallery, lastDeleted, top);

                            Trace.TraceInformation("FEED DeletedPackages: {0}", deletedPackages.Count);

                            lastDeleted = await Deletes2Catalog(deletedPackages, catalogStorage, lastCreated, lastEdited, lastDeleted, false, cancellationToken);
                            if (previousLastDeleted == lastDeleted)
                            {
                                break;
                            }
                            previousLastDeleted = lastDeleted;
                        }
                        while (deletedPackages.Count > 0);
                    }
                }

                Thread.Sleep(interval * 1000);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng feed2catalog -gallery <v2-feed-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] -storageTypeAuditing file|azure [-storagePathAuditing <path>]|[-storageAccountNameAuditing <azure-acc> -storageKeyValueAuditing <azure-key> -storageContainerAuditing <azure-container> -storagePathAuditing <path>]  [-verbose true|false] [-interval <seconds>] [-startDate <DateTime>]");
        }

        public static void Run(string[] args, CancellationToken cancellationToken)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            string gallery = CommandHelpers.GetGallery(arguments);
            if (gallery == null)
            {
                PrintUsage();
                return;
            }

            bool verbose = CommandHelpers.GetVerbose(arguments);

            int interval = CommandHelpers.GetInterval(arguments);

            DateTime startDate = CommandHelpers.GetStartDate(arguments);

            StorageFactory catalogStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            StorageFactory auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);
            if (catalogStorageFactory == null || auditingStorageFactory == null)
            {
                PrintUsage();
                return;
            }

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            Trace.TraceInformation("CONFIG source: \"{0}\" storage: \"{1}\" interval: {2}", gallery, catalogStorageFactory, interval);
            DateTime? nullableStartDate = null;
            if (startDate != DateTime.MinValue)
            {
                nullableStartDate = startDate;
            }
            Loop(gallery, catalogStorageFactory, auditingStorageFactory, verbose, interval, nullableStartDate, cancellationToken).Wait();
        }

        static void PackagePrintUsage()
        {
            Console.WriteLine("Usage: ng package2catalog -gallery <v2-feed-address> -storageBaseAddress <storage-base-address> -storageType file|azure [-storagePath <path>]|[-storageAccountName <azure-acc> -storageKeyValue <azure-key> -storageContainer <azure-container> -storagePath <path>] [-verbose true|false] -id <id> [-version <version>]");
        }

        public static void Package(string[] args, CancellationToken cancellationToken)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PackagePrintUsage();
                return;
            }

            string gallery = CommandHelpers.GetGallery(arguments);
            if (gallery == null)
            {
                PackagePrintUsage();
                return;
            }

            bool verbose = CommandHelpers.GetVerbose(arguments);

            string id = CommandHelpers.GetId(arguments);
            if (id == null)
            {
                PackagePrintUsage();
                return;
            }

            string version = CommandHelpers.GetVersion(arguments);

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            if (storageFactory == null)
            {
                PrintUsage();
                return;
            }

            if (verbose)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
                Trace.AutoFlush = true;
            }

            ProcessPackages(gallery, storageFactory, id, version, verbose, cancellationToken).Wait();
        }

        static async Task ProcessPackages(string gallery, StorageFactory storageFactory, string id, string version, bool verbose, CancellationToken cancellationToken)
        {
            int timeout = 300;

            Func<HttpMessageHandler> handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            HttpMessageHandler handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);

                //  if the version is specified a single package is processed otherwise all the packages corresponding to that id are processed

                Uri uri = (version == null) ? MakePackageUri(gallery, id) : MakePackageUri(gallery, id, version);

                SortedList<DateTime, IList<Tuple<Uri, FeedDetails>>> packages = await GetPackages(client, uri, "Created");

                Trace.TraceInformation("downloading {0} packages", packages.Select(t => t.Value.Count).Sum());

                Storage storage = storageFactory.Create();

                //  the idea here is to leave the lastCreated and lastEdited values exactly as they were

                const string LastCreated = "nuget:lastCreated";
                const string LastEdited = "nuget:lastEdited";
                const string LastDeleted = "nuget:lastDeleted";

                DateTime lastCreated = await GetCatalogProperty(storage, LastCreated, cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                DateTime lastEdited = await GetCatalogProperty(storage, LastEdited, cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                DateTime lastDeleted = await GetCatalogProperty(storage, LastDeleted, cancellationToken) ?? DateTime.MinValue.ToUniversalTime();

                DateTime d = await DownloadMetadata2Catalog(client, packages, storage, lastCreated, lastEdited, lastDeleted, null, cancellationToken);
            }
        }
    }
}
