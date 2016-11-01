// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ng.Jobs
{
    /// <summary>
    /// Utility class to share functionality between Package2Catalog and Feed2Catalog.
    /// </summary>
    public static class CatalogUtility
    {
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

        public static HttpClient CreateHttpClient(bool verbose)
        {
            var handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            var handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            return new HttpClient(handler);
        }

        public static async Task<DateTime?> GetCatalogProperty(Storage storage, string propertyName, CancellationToken cancellationToken)
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

        public static async Task<SortedList<DateTime, IList<PackageDetails>>> GetPackages(HttpClient client, Uri uri, string keyDateProperty)
        {
            const string createdDateProperty = "Created";
            const string lastEditedDateProperty = "LastEdited";
            const string publishedDateProperty = "Published";
            const string licenseNamesProperty = "LicenseNames";
            const string licenseReportUrlProperty = "LicenseReportUrl";

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

                var createdElement = propertiesElement.Element(dataservices + createdDateProperty);
                var createdValue = createdElement?.Value;
                var createdDate = string.IsNullOrEmpty(createdValue) ? DateTime.MinValue : DateTime.Parse(createdValue);

                var lastEditedValue = propertiesElement.Element(dataservices + lastEditedDateProperty).Value;
                var lastEditedDate = string.IsNullOrEmpty(lastEditedValue) ? DateTime.MinValue : DateTime.Parse(lastEditedValue);

                var publishedValue = propertiesElement.Element(dataservices + publishedDateProperty).Value;
                var publishedDate = string.IsNullOrEmpty(publishedValue) ? createdDate : DateTime.Parse(publishedValue);

                var keyEntryValue = propertiesElement.Element(dataservices + keyDateProperty).Value;
                var keyDate = string.IsNullOrEmpty(keyEntryValue) ? createdDate : DateTime.Parse(keyEntryValue);

                // License details
                var licenseNamesElement = propertiesElement.Element(dataservices + licenseNamesProperty);
                var licenseNames = licenseNamesElement?.Value;

                var licenseReportUrlElement = propertiesElement.Element(dataservices + licenseReportUrlProperty);
                var licenseReportUrl = licenseReportUrlElement?.Value;

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

        private static DateTime ForceUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }
            return date;
        }

        public static async Task<DateTime> DownloadMetadata2Catalog(HttpClient client, SortedList<DateTime, IList<PackageDetails>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, bool? createdPackages, CancellationToken cancellationToken, ILogger logger)
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

                                logger?.LogInformation("Add metadata from: {PackageDetailsContentUri}", packageItem.ContentUri);
                            }
                            else
                            {
                                logger?.LogWarning("Unable to extract metadata from: {PackageDetailsContentUri}", packageItem.ContentUri);
                            }
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            //  the feed is out of sync with the actual package storage - if we don't have the package there is nothing to be done we might as well move onto the next package
                            logger?.LogWarning("Unable to download: {PackageDetailsContentUri}. Http status: {HttpStatusCode}", packageItem.ContentUri, response.StatusCode);
                        }
                        else
                        {
                            //  this should trigger a restart - of this program - and not move the cursor forward
                            logger?.LogError("Unable to download: {PackageDetailsContentUri}. Http status: {HttpStatusCode}", packageItem.ContentUri, response.StatusCode);
                            throw new Exception(
                                $"Unable to download: {packageItem.ContentUri} http status: {response.StatusCode}");
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

            logger?.LogInformation("COMMIT metadata to catalog.");

            return lastDate;
        }

        private static DateTime DetermineLastDate(DateTime lastCreated, DateTime lastEdited, bool? createdPackages)
        {
            if (!createdPackages.HasValue)
            {
                return DateTime.MinValue;
            }
            return createdPackages.Value ? lastCreated : lastEdited;
        }
    }
}
