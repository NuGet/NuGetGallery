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
            const string idProperty = "Id";
            const string normalizedVersionProperty = "NormalizedVersion";
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

                var packageIdElement = propertiesElement.Element(dataservices + idProperty);
                var packageId = packageIdElement?.Value;
                var packageNormalizedVersionElement = propertiesElement.Element(dataservices + normalizedVersionProperty);
                var packageNormalizedVersion = packageNormalizedVersionElement?.Value;

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
                createdDate = createdDate.ForceUtc();
                lastEditedDate = lastEditedDate.ForceUtc();
                publishedDate = publishedDate.ForceUtc();

                packages.Add(new FeedPackageDetails(
                    content,
                    createdDate,
                    lastEditedDate,
                    publishedDate,
                    packageId,
                    packageNormalizedVersion,
                    licenseNames,
                    licenseReportUrl));
            }

            return packages;
        }

        /// <summary>
        /// Asynchronously writes package metadata to the catalog.
        /// </summary>
        /// <param name="packageCatalogItemCreator">A package catalog item creator.</param>
        /// <param name="packages">Packages to download metadata for.</param>
        /// <param name="storage">Storage.</param>
        /// <param name="lastCreated">The catalog's last created datetime.</param>
        /// <param name="lastEdited">The catalog's last edited datetime.</param>
        /// <param name="lastDeleted">The catalog's last deleted datetime.</param>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism for package processing.</param>
        /// <param name="createdPackages"><c>true</c> to include created packages; otherwise, <c>false</c>.</param>
        /// <param name="updateCreatedFromEdited"><c>true</c> to update the created cursor from the last edited cursor;
        /// otherwise, <c>false</c>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="telemetryService">A telemetry service.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns the latest
        /// <see cref="DateTime}" /> that was processed.</returns>
        public static async Task<DateTime> DownloadMetadata2CatalogAsync(
            IPackageCatalogItemCreator packageCatalogItemCreator,
            SortedList<DateTime, IList<FeedPackageDetails>> packages,
            IStorage storage,
            DateTime lastCreated,
            DateTime lastEdited,
            DateTime lastDeleted,
            int maxDegreeOfParallelism,
            bool? createdPackages,
            bool updateCreatedFromEdited,
            CancellationToken cancellationToken,
            ITelemetryService telemetryService,
            ILogger logger)
        {
            if (packageCatalogItemCreator == null)
            {
                throw new ArgumentNullException(nameof(packageCatalogItemCreator));
            }

            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var writer = new AppendOnlyCatalogWriter(storage, telemetryService, Constants.MaxPageSize);

            var lastDate = DetermineLastDate(lastCreated, lastEdited, createdPackages);

            if (packages.Count == 0)
            {
                return lastDate;
            }

            // Flatten the sorted list.
            var workItems = packages.SelectMany(
                    pair => pair.Value.Select(
                        details => new PackageWorkItem(pair.Key, details)))
                .ToArray();

            await workItems.ForEachAsync(maxDegreeOfParallelism, async workItem =>
            {
                workItem.PackageCatalogItem = await packageCatalogItemCreator.CreateAsync(
                    workItem.FeedPackageDetails,
                    workItem.Timestamp,
                    cancellationToken);
            });

            lastDate = packages.Last().Key;

            // AppendOnlyCatalogWriter.Add(...) is not thread-safe, so add them all at once on one thread.
            foreach (var workItem in workItems.Where(workItem => workItem.PackageCatalogItem != null))
            {
                writer.Add(workItem.PackageCatalogItem);

                logger?.LogInformation("Add metadata from: {PackageDetailsContentUri}", workItem.FeedPackageDetails.ContentUri);
            }

            if (createdPackages.HasValue)
            {
                lastEdited = !createdPackages.Value ? lastDate : lastEdited;

                if (updateCreatedFromEdited)
                {
                    lastCreated = lastEdited;
                }
                else
                {
                    lastCreated = createdPackages.Value ? lastDate : lastCreated;
                }
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

        private sealed class PackageWorkItem
        {
            internal DateTime Timestamp { get; }
            internal FeedPackageDetails FeedPackageDetails { get; }
            internal PackageCatalogItem PackageCatalogItem { get; set; }

            internal PackageWorkItem(DateTime timestamp, FeedPackageDetails feedPackageDetails)
            {
                Timestamp = timestamp;
                FeedPackageDetails = feedPackageDetails;
            }
        }
    }
}