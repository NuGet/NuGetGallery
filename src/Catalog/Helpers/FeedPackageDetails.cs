// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public sealed class FeedPackageDetails
    {
        public Uri ContentUri { get; }
        public DateTime CreatedDate { get; }
        public DateTime LastEditedDate { get; }
        public DateTime PublishedDate { get; }
        public string PackageId { get; }
        public string PackageNormalizedVersion { get; }
        public string PackageFullVersion { get; }
        public string LicenseNames { get; }
        public string LicenseReportUrl { get; }
        public bool RequiresLicenseAcceptance { get; }
        public PackageDeprecationItem DeprecationInfo { get; }
        public IList<PackageVulnerabilityItem> VulnerabilityInfo { get; private set; }

        public bool HasDeprecationInfo => DeprecationInfo != null;

        public FeedPackageDetails(
            Uri contentUri,
            DateTime createdDate,
            DateTime lastEditedDate,
            DateTime publishedDate,
            string packageId,
            string packageNormalizedVersion,
            string packageFullVersion)
            : this(
                contentUri,
                createdDate,
                lastEditedDate,
                publishedDate,
                packageId,
                packageNormalizedVersion,
                packageFullVersion,
                licenseNames: null,
                licenseReportUrl: null,
                deprecationInfo: null,
                requiresLicenseAcceptance: false)
        {
        }

        public FeedPackageDetails(
            Uri contentUri,
            DateTime createdDate,
            DateTime lastEditedDate,
            DateTime publishedDate,
            string packageId,
            string packageNormalizedVersion,
            string packageFullVersion,
            string licenseNames,
            string licenseReportUrl,
            PackageDeprecationItem deprecationInfo,
            bool requiresLicenseAcceptance)
        {
            ContentUri = contentUri;
            CreatedDate = createdDate;
            LastEditedDate = lastEditedDate;
            PublishedDate = publishedDate;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            PackageFullVersion = packageFullVersion;
            LicenseNames = licenseNames;
            LicenseReportUrl = licenseReportUrl;
            DeprecationInfo = deprecationInfo;
            RequiresLicenseAcceptance = requiresLicenseAcceptance;
        }

        public void AddVulnerability(PackageVulnerabilityItem vulnerability)
        {
            VulnerabilityInfo = VulnerabilityInfo ?? new List<PackageVulnerabilityItem>();
            VulnerabilityInfo.Add(vulnerability);
        }
    }
}