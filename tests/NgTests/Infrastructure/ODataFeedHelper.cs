// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NgTests.Infrastructure
{
    public static class ODataFeedHelper
    {
        public static string ToODataFeed(IEnumerable<ODataPackage> packages, Uri baseUri, string title)
        {
            string nsAtom = "http://www.w3.org/2005/Atom";
            var id = string.Format(CultureInfo.InvariantCulture, "{0}{1}", baseUri, title);
            XDocument doc = new XDocument(
                new XElement(XName.Get("feed", nsAtom),
                    new XElement(XName.Get("id", nsAtom), id),
                    new XElement(XName.Get("title", nsAtom), title)));

            foreach (var package in packages)
            {
                doc.Root.Add(ToODataEntryXElement(package, baseUri));
            }

            return doc.ToString();
        }

        private static XElement ToODataEntryXElement(ODataPackage package, Uri baseUri)
        {
            string nsAtom = "http://www.w3.org/2005/Atom";
            XNamespace nsDataService = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            string nsMetadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
            string downloadUrl = string.Format(
                CultureInfo.InvariantCulture,
                "{0}package/{1}/{2}", baseUri, package.Id, NuGetVersionUtility.NormalizeVersion(package.Version));
            string entryId = string.Format(
                CultureInfo.InvariantCulture,
                "{0}Packages(Id='{1}',Version='{2}')",
                baseUri, package.Id, NuGetVersionUtility.NormalizeVersion(package.Version));

            const string FeedDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFF";

            var entry = new XElement(XName.Get("entry", nsAtom),
                new XAttribute(XNamespace.Xmlns + "d", nsDataService.ToString()),
                new XAttribute(XNamespace.Xmlns + "m", nsMetadata),
                new XElement(XName.Get("id", nsAtom), entryId),
                new XElement(XName.Get("title", nsAtom), package.Id),
                new XElement(XName.Get("content", nsAtom),
                    new XAttribute("type", "application/zip"),
                    new XAttribute("src", downloadUrl)),
                new XElement(XName.Get("properties", nsMetadata),
                    new XElement(nsDataService + "Id", package.Id),
                    new XElement(nsDataService + "NormalizedVersion", package.Version),
                    new XElement(nsDataService + "Version", package.Version),
                    new XElement(nsDataService + "PackageHash", "dummy"),
                    new XElement(nsDataService + "PackageHashAlgorithm", "dummy"),
                    new XElement(nsDataService + "Description", package.Description),
                    new XElement(nsDataService + "Listed", package.Listed),


                    new XElement(nsDataService + "Created", package.Created.ToString(FeedDateTimeFormat)),
                    new XElement(nsDataService + "LastEdited", package.LastEdited?.ToString(FeedDateTimeFormat)),
                    new XElement(nsDataService + "Published", package.Published.ToString(FeedDateTimeFormat)),
                    new XElement(nsDataService + "LicenseNames", package.LicenseNames),
                    new XElement(nsDataService + "LicenseReportUrl", package.LicenseReportUrl)));

            return entry;
        }
        
        public static string ToOData(ODataPackage package, Uri baseUri)
        {
            XDocument doc = new XDocument(ToODataEntryXElement(package, baseUri));
            return doc.ToString();
        }
    }
}