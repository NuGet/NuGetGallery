// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageRecommendationService : IPackageRecommendationService
    {
        private readonly IReportService _reportService;

        public PackageRecommendationService(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IEnumerable<RecommendedPackageViewModel>> GetRecommendedPackagesAsync(Package package)
        {
            int pageNumber;
            string encodedId = GetHexString(Encoding.UTF8.GetBytes(package.Title));
            string reportName = $"page{pageNumber}/{encodedId}.json";
            var report = await _reportService.Load(reportName);

            string targetId = report["Id"];
            Debug.Assert(targetId == package.Title);

            return report["Recommendations"].Select(
                r => GetViewModel(recommendationId: (string)r));
        }

        private static string GetHexString(byte[] bytes)
        {
            var sb = new StringBuilder(capacity: bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static RecommendedPackageViewModel GetViewModel(string recommendationId)
        {
            return new RecommendedPackageViewModel
            {
                Id = recommendationId,
                GalleryPageUrl = $"https://www.nuget.org/packages/{recommendationId}",
                IconUrl = "" // TODO
            };
        }
    }
}
