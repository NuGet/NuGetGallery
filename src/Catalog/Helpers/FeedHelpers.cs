// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Helper methods for accessing and parsing the gallery's V2 feed.
    /// </summary>
    public static class FeedHelpers
    {
        /// <summary>
        /// Creates an HttpClient for reading the feed.
        /// </summary>
        public static HttpClient CreateHttpClient(Func<HttpMessageHandler> handlerFunc)
        {
            var handler = (handlerFunc != null) ? handlerFunc() : new WebRequestHandler { AllowPipelining = true };
            return new HttpClient(handler);
        }

        /// <summary>
        /// Asynchronously reads and returns top-level <see cref="DateTime" /> metadata from the catalog's index.json.
        /// </summary>
        /// <remarks>The metadata values include "nuget:lastCreated", "nuget:lastDeleted", and "nuget:lastEdited",
        /// which are the timestamps of the catalog cursor.</remarks>
        /// <param name="storage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="CatalogProperties" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="storage" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public static async Task<CatalogProperties> GetCatalogPropertiesAsync(
            IStorage storage,
            CancellationToken cancellationToken)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            cancellationToken.ThrowIfCancellationRequested();

            DateTime? lastCreated = null;
            DateTime? lastDeleted = null;
            DateTime? lastEdited = null;

            var json = await storage.LoadString(storage.ResolveUri("index.json"), cancellationToken);

            if (json != null)
            {
                var obj = JObject.Parse(json);
                JToken token;

                if (obj.TryGetValue("nuget:lastCreated", out token))
                {
                    lastCreated = token.ToObject<DateTime>().ToUniversalTime();
                }

                if (obj.TryGetValue("nuget:lastDeleted", out token))
                {
                    lastDeleted = token.ToObject<DateTime>().ToUniversalTime();
                }

                if (obj.TryGetValue("nuget:lastEdited", out token))
                {
                    lastEdited = token.ToObject<DateTime>().ToUniversalTime();
                }
            }

            return new CatalogProperties(lastCreated, lastDeleted, lastEdited);
        }

        /// <summary>
        /// Builds a <see cref="Uri"/> for accessing the metadata of a specific package on the feed.
        /// </summary>
        public static Uri MakeUriForPackage(string source, string id, string version)
        {
            var uri = new Uri($"{source.Trim('/')}/Packages(Id='{HttpUtility.UrlEncode(id)}',Version='{HttpUtility.UrlEncode(version)}')");
            return UriUtils.GetNonhijackableUri(uri);
        }

        /// <summary>
        /// Returns a <see cref="SortedList{DateTime, IList{FeedPackageDetails}}"/> from the feed.
        /// </summary>
        /// <param name="keyDateFunc">The <see cref="DateTime"/> field to sort the <see cref="FeedPackageDetails"/> on.</param>
        public static async Task<SortedList<DateTime, IList<FeedPackageDetails>>> GetPackagesInOrder(HttpClient client, Uri uri, Func<FeedPackageDetails, DateTime> keyDateFunc)
        {
            var result = new SortedList<DateTime, IList<FeedPackageDetails>>();

            var allPackages = await GetPackages(client, uri);

            foreach (var package in allPackages)
            {
                IList<FeedPackageDetails> packagesWithSameKeyDate;

                var packageKeyDate = keyDateFunc(package);
                if (!result.TryGetValue(packageKeyDate, out packagesWithSameKeyDate))
                {
                    packagesWithSameKeyDate = new List<FeedPackageDetails>();
                    result.Add(packageKeyDate, packagesWithSameKeyDate);
                }

                packagesWithSameKeyDate.Add(package);
            }

            return result;
        }

        /// <summary>
        /// Returns a <see cref="FeedPackageDetails"/> for a single package from the feed.
        /// </summary>
        public static async Task<FeedPackageDetails> GetPackage(HttpClient client, string source, string id, string version)
        {
            return (await GetPackages(client, MakeUriForPackage(source, id, version))).FirstOrDefault();
        }

        /// <summary>
        /// Asynchronously gets a <see cref="IList{FeedPackageDetails}"/> from the feed.
        /// </summary>
        /// <param name="client">An HTTP client.</param>
        /// <param name="uri">The feed URI.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IList{FeedPackageDetails}" />.</returns>
        public static async Task<IList<FeedPackageDetails>> GetPackages(HttpClient client, Uri uri)
        {
            const string createdDateProperty = "Created";
            const string lastEditedDateProperty = "LastEdited";
            const string publishedDateProperty = "Published";
            const string licenseNamesProperty = "LicenseNames";
            const string licenseReportUrlProperty = "LicenseReportUrl";

            var packages = new List<FeedPackageDetails>();

            XElement feed;
            try
            {
                using (var stream = await client.GetStreamAsync(uri))
                {
                    feed = XElement.Load(stream);
                }
            }
            catch (TaskCanceledException tce)
            {
                // If the HTTP request timed out, a TaskCanceledException will be thrown.
                throw new HttpClientTimeoutException($"HttpClient request timed out in {nameof(FeedHelpers.GetPackages)}.", tce);
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

                packages.Add(new FeedPackageDetails
                {
                    ContentUri = content,
                    CreatedDate = createdDate,
                    LastEditedDate = lastEditedDate,
                    PublishedDate = publishedDate,
                    LicenseNames = licenseNames,
                    LicenseReportUrl = licenseReportUrl
                });
            }

            return packages;
        }

        private static DateTime ForceUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }
            return date;
        }

        /// <summary>
        /// Asynchronously writes package metadata to the catalog.
        /// </summary>
        /// <param name="client">An HTTP client.</param>
        /// <param name="packages">Packages to download metadata for.</param>
        /// <param name="storage">Storage.</param>
        /// <param name="lastCreated">The catalog's last created datetime.</param>
        /// <param name="lastEdited">The catalog's last edited datetime.</param>
        /// <param name="lastDeleted">The catalog's last deleted datetime.</param>
        /// <param name="createdPackages"><c>true</c> to include created packages; otherwise, <c>false</c>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns the latest
        /// <see cref="DateTime}" /> that was processed.</returns>
        public static async Task<DateTime> DownloadMetadata2Catalog(
            HttpClient client,
            SortedList<DateTime, IList<FeedPackageDetails>> packages,
            IStorage storage,
            DateTime lastCreated,
            DateTime lastEdited,
            DateTime lastDeleted,
            bool? createdPackages,
            CancellationToken cancellationToken,
            ILogger logger)
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
                    HttpResponseMessage response = null;
                    try
                    {
                        response = await client.GetAsync(packageUri, cancellationToken);
                    }
                    catch (TaskCanceledException tce)
                    {
                        // If the HTTP request timed out, a TaskCanceledException will be thrown.
                        throw new HttpClientTimeoutException($"HttpClient request timed out in {nameof(FeedHelpers.DownloadMetadata2Catalog)}.", tce);
                    }

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