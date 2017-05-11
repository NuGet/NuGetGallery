// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        private DateTime? _lastUpdatedUtc;

        public StatisticsPackagesViewModel()
        {
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll { get; set; }

        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion { get; set; }

        public IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks { get; set; }

        public bool IsDownloadPackageAvailable { get; set; }

        public bool IsDownloadPackageDetailAvailable { get; set; }

        public bool IsNuGetClientVersionAvailable { get; set; }

        public bool IsLast6WeeksAvailable { get; set; }

        public int NuGetClientVersionTotalDownloads { get; private set; }

        public string PackageId { get; private set; }

        public string PackageVersion { get; private set; }

        public bool UseD3 { get; set; }

        public DateTime? LastUpdatedUtc
        {
            get { return _lastUpdatedUtc; }
            set { _lastUpdatedUtc = value; }
        }

        public void SetPackageDownloadsByVersion(string packageId)
        {
            PackageId = packageId;
        }

        public void SetPackageVersionDownloadsByClient(string packageId, string packageVersion)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "We want to be able to use this easily in the related view.")]
        public string DisplayDownloads(int downloads)
        {
            return downloads.ToNuGetNumberString();
        }

        public void Update()
        {
            if (IsNuGetClientVersionAvailable)
            {
                NuGetClientVersionTotalDownloads = NuGetClientVersion.Sum(item => item.Downloads);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "We want to be able to use this easily in the related view.")]
        public string DisplayWeek(int year, int weekOfYear)
        {
            if (weekOfYear < 1 || weekOfYear > 53)
            {
                return string.Empty;
            }
            return string.Format(CultureInfo.CurrentCulture, "{0} wk {1}", year, weekOfYear);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "We want to be able to use this easily in the related view.")]
        public string DisplayPercentage(float amount, float total)
        {
            return (amount / total).ToString("P0", CultureInfo.CurrentCulture);
        }
    }
}
