// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

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
                    licenseReportUrl,
                    deprecationInfo: null));
            }

            return packages;
        }
    }
}