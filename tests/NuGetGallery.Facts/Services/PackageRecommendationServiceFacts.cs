// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery
{
    public class PackageRecommendationServiceFacts
    {
        private static RelatedPackagesService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IReportService> reportService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            reportService = reportService ?? new Mock<IReportService>();

            return new RelatedPackagesService(
                packageService.Object,
                reportService.Object);
        }

        private static Mock<IPackageService> CreatePackageService(IEnumerable<string> packageIds)
        {
            var packageService = new Mock<IPackageService>();
            packageService
                .Setup(ps => ps.FindAbsoluteLatestPackageById(
                    /* id */ It.IsIn(packageIds),
                    /* semVerLevelKey */ null))
                .Returns<string, int?>((id, _) => CreatePackage(id));
            return packageService;
        }

        private static Mock<IReportService> CreateReportService(
            params RelatedPackages[] recommendationSets)
        {
            var targetIds = recommendationSets.Select(rp => rp.Id);
            var idMap = targetIds.ToDictionary(
                keySelector: id => RelatedPackagesService.GetReportName(id),
                elementSelector: id => id);
            var reportMap = recommendationSets.ToDictionary(
                keySelector: rp => rp.Id,
                elementSelector: rp => CreateReport(rp.Id, rp.Recommendations));

            Task<ReportBlob> GetReportByName(string reportName)
            {
                var targetId = idMap[reportName];
                return Task.FromResult(reportMap[targetId]);
            }

            var reportNames = idMap.Keys;
            var reportService = new Mock<IReportService>();
            reportService
                .Setup(rs => rs.Load(It.IsIn<string>(reportNames)))
                .Returns<string>(GetReportByName);
            return reportService;
        }

        private static Package CreatePackage(string packageId)
        {
            var packageRegistration = new PackageRegistration { Id = packageId };
            return new Package { PackageRegistration = packageRegistration };
        }

        private static ReportBlob CreateReport(string id, IEnumerable<string> recommendations)
        {
            string json = JsonConvert.SerializeObject(new { id, recommendations });
            return new ReportBlob(json);
        }

        public class TheGetRecommendedPackagesMethod
        {
            [Fact]
            public async void WillReturnEmptyListIfReportIsNotFound()
            {
                var reportService = new Mock<IReportService>();
                reportService
                    .Setup(rs => rs.Load(It.IsAny<string>()))
                    .ThrowsAsync(new ReportNotFoundException());
                var recommendationService = CreateService(
                    reportService: reportService);

                var result = await recommendationService.GetRelatedPackagesAsync(CreatePackage("Newtonsoft.Json"));

                Assert.Empty(result);
            }

            [Fact]
            public async void WillReturnPackagesListedInReport()
            {
                string targetId = "Newtonsoft.Json";
                var recommendationIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var allIds = new[] { targetId }.Concat(recommendationIds);

                var packageService = CreatePackageService(allIds);
                var reportService = CreateReportService(
                    new RelatedPackages
                    {
                        Id = targetId,
                        Recommendations = recommendationIds
                    });

                var recommendationService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = (await recommendationService
                    .GetRelatedPackagesAsync(CreatePackage(targetId)))
                    .Select(p => p.PackageRegistration.Id);

                Assert.Equal(recommendationIds, result);
            }

            [Fact]
            public async void WillNotReturnPackagesNoLongerInDatabase()
            {
                string targetId = "Newtonsoft.Json";
                string excludedId = "SammysJsonLibrary";
                var recommendationIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var registeredIds = new[] { targetId }.Concat(recommendationIds).Except(new[] { excludedId });

                var packageService = CreatePackageService(registeredIds);
                var reportService = CreateReportService(
                    new RelatedPackages
                    {
                        Id = targetId,
                        Recommendations = recommendationIds
                    });

                var recommendationService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = (await recommendationService
                    .GetRelatedPackagesAsync(CreatePackage(targetId)))
                    .Select(p => p.PackageRegistration.Id);

                Assert.Equal(recommendationIds.Intersect(registeredIds), result);
            }
        }
    }
}
