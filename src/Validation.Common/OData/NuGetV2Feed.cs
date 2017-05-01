// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Jobs.Validation.Common.OData
{
    public class NuGetV2Feed
    {
        private readonly HttpClient _httpClient;

        public NuGetV2Feed(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<NuGetPackage>> GetPackagesAsync(Uri uri, int continuationsToFollow = 0)
        {
            Trace.TraceInformation("Start retrieving packages from URL {0}...", uri);

            var result = new List<NuGetPackage>();

            XElement feed;
            using (var stream = await _httpClient.GetStreamAsync(uri))
            {
                feed = XElement.Load(stream);
            }

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dataservices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            foreach (var entry in feed.Elements(atom + "entry"))
            {
                var propertiesElement = entry.Element(metadata + "properties");

                var package = new NuGetPackage
                {
                    Id = entry.GetString(atom + "title", null),
                    Version = propertiesElement.GetString(dataservices + "Version", null),
                    NormalizedVersion = propertiesElement.GetString(dataservices + "NormalizedVersion", null),
                    DownloadUrl = new Uri(entry.Element(atom + "content").Attribute("src").Value),
                    Copyright = propertiesElement.GetString(dataservices + "Copyright", null),
                    Created = propertiesElement.GetDateTimeOffset(dataservices + "Created"),
                    Dependencies = propertiesElement.GetString(dataservices + "Dependencies", null),
                    Description = propertiesElement.GetString(dataservices + "Description", null),
                    DownloadCount = propertiesElement.GetInt32(dataservices + "DownloadCount", 0),
                    VersionDownloadCount = propertiesElement.GetInt32(dataservices + "VersionDownloadCount", 0),
                    GalleryDetailsUrl = propertiesElement.GetUri(dataservices + "GalleryDetailsUrl", null),
                    IconUrl = propertiesElement.GetUri(dataservices + "IconUrl", null),
                    IsAbsoluteLatestVersion = propertiesElement.GetBool(dataservices + "IsAbsoluteLatestVersion", false),
                    IsLatestVersion = propertiesElement.GetBool(dataservices + "IsLatestVersion", false),
                    IsPrerelease = propertiesElement.GetBool(dataservices + "IsPrerelease", false),
                    Language = propertiesElement.GetString(dataservices + "Language", null),
                    LastEdited = propertiesElement.GetDateTimeOffset(dataservices + "LastEdited", null),
                    LicenseNames = propertiesElement.GetString(dataservices + "LicenseNames", null),
                    LicenseReportUrl = propertiesElement.GetUri(dataservices + "LicenseReportUrl", null),
                    LicenseUrl = propertiesElement.GetUri(dataservices + "LicenseUrl", null),
                    MinClientVersion = propertiesElement.GetString(dataservices + "MinClientVersion", null),
                    PackageHash = propertiesElement.GetString(dataservices + "PackageHash", null),
                    PackageHashAlgorithm = propertiesElement.GetString(dataservices + "PackageHashAlgorithm", null),
                    PackageSize = propertiesElement.GetInt64(dataservices + "PackageSize", 0),
                    ProjectUrl = propertiesElement.GetUri(dataservices + "ProjectUrl", null),
                    Published = propertiesElement.GetDateTimeOffset(dataservices + "Published", null),
                    ReleaseNotes = propertiesElement.GetString(dataservices + "ReleaseNotes", null),
                    ReportAbuseUrl = propertiesElement.GetUri(dataservices + "ReportAbuseUrl", null),
                    RequireLicenseAcceptance = propertiesElement.GetBool(dataservices + "RequireLicenseAcceptance", false),
                    Summary = entry.GetString(atom + "summary", null),
                    Tags = propertiesElement.GetString(dataservices + "Tags", null),
                    Title = propertiesElement.GetString(dataservices + "Title", null)
                };

                result.Add(package);
            }

            Trace.TraceInformation("Finished retrieving packages from URL {0}.", uri);
            
            if (continuationsToFollow > 0)
            {
                var links = feed.Elements(atom + "link");
                var continuationLink = links.FirstOrDefault(
                        link => link.Attribute("rel") != null && link.Attribute("rel").Value == "next");

                if (continuationLink != null)
                {
                    var href = continuationLink.Attribute("href").Value;

                    Trace.TraceInformation("Start following continuation token {0}...", href);
                    result.AddRange(await GetPackagesAsync(new Uri(href), continuationsToFollow - 1));
                    Trace.TraceInformation("Finished following continuation token {0}.", href);
                }
            }

            return result;
        }
    }
}