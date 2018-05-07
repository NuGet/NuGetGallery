// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class PackageRecommendationService : IPackageRecommendationService
    {
        private readonly IPackageService _packageService;
        private readonly IReportService _reportService;

        public PackageRecommendationService(
            IPackageService packageService,
            IReportService reportService)
        {
            _packageService = packageService;
            _reportService = reportService;
        }

        public async Task<IEnumerable<ListPackageItemViewModel>> GetRecommendedPackagesAsync(Package package)
        {
            string reportName = GetReportName(package);
            ReportBlob report;
            try
            {
                report = await _reportService.Load(reportName);
            }
            catch (ReportNotFoundException ex)
            {
                QuietLog.LogHandledException(ex);
                return Enumerable.Empty<ListPackageItemViewModel>();
            }
            var recommendedPackages = JsonConvert.DeserializeObject<RecommendedPackages>(report.Content);

            string targetId = recommendedPackages.Id;
            Debug.Assert(string.Equals(targetId, package.Title, StringComparison.OrdinalIgnoreCase));

            var recommendationIds = recommendedPackages.Recommendations;
            return recommendationIds.Select(
                id => new ListPackageItemViewModel(
                    _packageService.FindAbsoluteLatestPackageById(id),
                    // We don't need to know about the user's permissions for any of the recommended packages.
                    currentUser: null));
        }

        private static string GetReportName(Package package)
        {
            string GetHexadecimalString(byte[] bytes)
            {
                var sb = new StringBuilder(capacity: bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }

            string encodedId = GetHexadecimalString(Encoding.UTF8.GetBytes(package.Title));
            return $"Recommendations/{encodedId}.json";
        }
    }
}
