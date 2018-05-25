// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery
{
    public class RelatedPackagesServiceFacts
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
            params RelatedPackages[] relatedPackageSets)
        {
            var targetIds = relatedPackageSets.Select(rp => rp.Id);
            var idMap = targetIds.ToDictionary(
                keySelector: id => RelatedPackagesService.GetReportName(id),
                elementSelector: id => id);
            var reportMap = relatedPackageSets.ToDictionary(
                keySelector: rp => rp.Id,
                elementSelector: rp => CreateReport(rp.Id, rp.Recommendations));

            Task<ReportBlob> GetReportByName(string reportName)
            {
                var targetId = idMap[reportName];
                return Task.FromResult(reportMap[targetId]);
            }

            var reportNames = idMap.Keys;
            var reportContainer = new Mock<IReportContainer>();
            reportContainer
                .Setup(rs => rs.Load(It.IsIn<string>(reportNames)))
                .Returns<string>(GetReportByName);
            return CreateReportService(reportContainer);
        }

        private static Mock<IReportService> CreateReportService(
            Mock<IReportContainer> reportContainer,
            string containerName = RelatedPackagesService.ContainerName)
        {
            var reportService = new Mock<IReportService>();
            reportService
                .Setup(rs => rs.GetContainer(containerName))
                .Returns(reportContainer.Object);
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

        public class TheGetRelatedPackagesMethod
        {
            [Fact]
            public async void WillReturnEmptyListIfReportIsNotFound()
            {
                var reportContainer = new Mock<IReportContainer>();
                reportContainer
                    .Setup(rs => rs.Load(It.IsAny<string>()))
                    .ThrowsAsync(new ReportNotFoundException());
                var reportService = CreateReportService(reportContainer);

                var relatedPackagesService = CreateService(
                    reportService: reportService);

                var result = await relatedPackagesService.GetRelatedPackagesAsync(CreatePackage("Newtonsoft.Json"));

                Assert.Empty(result);
            }

            [Fact]
            public async void WillReturnPackagesListedInReport()
            {
                string targetId = "Newtonsoft.Json";
                var relatedPackageIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var allIds = new[] { targetId }.Concat(relatedPackageIds);

                var packageService = CreatePackageService(allIds);
                var reportService = CreateReportService(
                    new RelatedPackages
                    {
                        Id = targetId,
                        Recommendations = relatedPackageIds
                    });

                var relatedPackagesService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = (await relatedPackagesService
                    .GetRelatedPackagesAsync(CreatePackage(targetId)))
                    .Select(p => p.PackageRegistration.Id);

                Assert.Equal(relatedPackageIds, result);
            }

            [Fact]
            public async void WillNotReturnPackagesNoLongerInDatabase()
            {
                string targetId = "Newtonsoft.Json";
                string excludedId = "SammysJsonLibrary";
                var relatedPackageIds = new[]
                {
                    "SammysJsonLibrary",
                    "ElliesJsonLibrary",
                    "JimmysJsonLibrary"
                };
                var registeredIds = new[] { targetId }.Concat(relatedPackageIds).Except(new[] { excludedId });

                var packageService = CreatePackageService(registeredIds);
                var reportService = CreateReportService(
                    new RelatedPackages
                    {
                        Id = targetId,
                        Recommendations = relatedPackageIds
                    });

                var relatedPackagesService = CreateService(
                    packageService: packageService,
                    reportService: reportService);

                var result = (await relatedPackagesService
                    .GetRelatedPackagesAsync(CreatePackage(targetId)))
                    .Select(p => p.PackageRegistration.Id);

                Assert.Equal(relatedPackageIds.Intersect(registeredIds), result);
            }
        }
    }
}
