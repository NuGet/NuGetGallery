// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public sealed class FeedPackageDetails
    {
        public Uri ContentUri { get; }
        public DateTime CreatedDate { get; }
        public DateTime LastEditedDate { get; }
        public DateTime PublishedDate { get; }
        public string PackageId { get; }
        public string PackageVersion { get; }
        public string LicenseNames { get; }
        public string LicenseReportUrl { get; }
        public PackageDeprecationItem DeprecationInfo { get; }

        public bool HasDeprecationInfo => DeprecationInfo != null;

        public FeedPackageDetails(
            Uri contentUri,
            DateTime createdDate,
            DateTime lastEditedDate,
            DateTime publishedDate,
            string packageId,
            string packageVersion)
            : this(
                contentUri,
                createdDate,
                lastEditedDate,
                publishedDate,
                packageId,
                packageVersion,
                licenseNames: null,
                licenseReportUrl: null,
                deprecationInfo: null)
        {
        }

        public FeedPackageDetails(
            Uri contentUri,
            DateTime createdDate,
            DateTime lastEditedDate,
            DateTime publishedDate,
            string packageId,
            string packageVersion,
            string licenseNames,
            string licenseReportUrl,
            PackageDeprecationItem deprecationInfo)
        {
            ContentUri = contentUri;
            CreatedDate = createdDate;
            LastEditedDate = lastEditedDate;
            PublishedDate = publishedDate;
            PackageId = packageId;
            PackageVersion = packageVersion;
            LicenseNames = licenseNames;
            LicenseReportUrl = licenseReportUrl;
            DeprecationInfo = deprecationInfo;
        }
    }
}