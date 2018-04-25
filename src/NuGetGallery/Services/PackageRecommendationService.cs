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
            string encodedId = GetHexadecimalString(Encoding.UTF8.GetBytes(package.Title));
            string reportName = $"{encodedId}.json";

            var report = await _reportService.Load(reportName);
            var reportJson = JObject.Parse(report.Content);

            string targetId = reportJson["Id"];
            Debug.Assert(targetId == package.Title);

            return reportJson["Recommendations"].Select(
                item => CreateViewModel(recommendationId: (string)item));
        }

        private static string GetHexadecimalString(byte[] bytes)
        {
            var sb = new StringBuilder(capacity: bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static RecommendedPackageViewModel CreateViewModel(string recommendationId)
        {
            string galleryPageUrl = $"https://www.nuget.org/packages/{recommendationId}";
            return new RecommendedPackageViewModel
            {
                Id = recommendationId,
                GalleryPageUrl = galleryPageUrl,
                IconUrl = "" // TODO
            };
        }
    }
}
