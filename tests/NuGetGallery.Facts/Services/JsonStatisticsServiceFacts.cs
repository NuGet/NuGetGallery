using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Services
{
    public class JsonStatisticsServiceFacts
    {
        public class TheRefreshMethod : FactsBase
        {
            [Fact]
            public async Task LoadsReports()
            {
                // Arrange
                Mock();

                // Act
                await _target.Refresh();

                // Assert
                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentpopularity.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularity.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentpopularitydetail.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularitydetail.json")), Times.Once);
            }

            [Fact]
            public void DoesntReloadReportsIfCached()
            {
                // Refresh method doesn't do anything second time
            }

            [Fact]
            public void LastUpdatedIsSmallestNonNullReportUpdateTime()
            {
                // LastUpdatedUtc is smallest non-null value
            }

            [Fact]
            public void LoadDownloadPackagesFailsIfPackageIdIsMissingOrNotString()
            {
            }

            [Fact]
            public void LoadDownloadPackagesFailsIDownloadsIsMissingOrNotInteger()
            {
            }

            [Fact]
            public void LoadDownloadPackageVersionsFailsIfPackageIdIsMissingOrNotString()
            {
            }

            [Fact]
            public void LoadDownloadPackageVersionsFailsIfVersionIsMissingOrNotString()
            {
            }

            [Fact]
            public void LoadDownloadPackageVersionsFailsIfDownloadsIsMissingOrNotInteger()
            {
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IReportService> _reportService;
            protected readonly JsonStatisticsService _target;

            protected readonly IEnumerable<Dictionary<string, object>> _downloadPackagesReport;
            protected readonly IEnumerable<Dictionary<string, object>> _downloadPackageVersionsReport;

            public FactsBase()
            {
                _reportService = new Mock<IReportService>();

                _target = new JsonStatisticsService(_reportService.Object);

                _downloadPackagesReport = new[]
                {
                    new Dictionary<string, object>()
                    {
                        { "PackageId", "A.Fantastic.Package" },
                        { "Downloads", 123 },
                    },

                    new Dictionary<string, object>()
                    {
                        { "PackageId", "Foo.Bar" },
                        { "Downloads", 456 },
                    },
                };

                _downloadPackageVersionsReport = new[]
                {
                    new Dictionary<string, object>()
                    {
                        { "PackageId", "A.Fantastic.Package" },
                        { "Version", "1.0.0" },
                        { "Downloads", 100 },
                    },

                    new Dictionary<string, object>()
                    {
                        { "PackageId", "A.Fantastic.Package" },
                        { "Version", "2.0.0" },
                        { "Downloads", 23 },
                    },

                    new Dictionary<string, object>()
                    {
                        { "PackageId", "Foo.Bar" },
                        { "Version", "1.0.0" },
                        { "Downloads", 456 },
                    },
                };
            }

            protected void Mock(
                IEnumerable<Dictionary<string, object>> packageDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> packageVersionDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> communityPackageDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> communityPackageVersionDownloadsReport = null,

                DateTime? packageDownloadsReportLastUpdateTimeUtc = null,
                DateTime? packageVersionDownloadsReportLastUpdateTimeUtc = null,
                DateTime? communityPackageDownloadsReportLastUpdateTimeUtc = null,
                DateTime? communityPackageVersionDownloadsReportLastUpdateTimeUtc = null)
            {
                packageDownloadsReport = packageDownloadsReport ?? _downloadPackagesReport;
                packageVersionDownloadsReport = packageVersionDownloadsReport ?? _downloadPackageVersionsReport;
                communityPackageDownloadsReport = communityPackageDownloadsReport ?? _downloadPackagesReport;
                communityPackageVersionDownloadsReport = communityPackageVersionDownloadsReport ?? _downloadPackageVersionsReport;

                packageDownloadsReportLastUpdateTimeUtc = packageDownloadsReportLastUpdateTimeUtc ?? DateTime.UtcNow;
                packageVersionDownloadsReportLastUpdateTimeUtc = packageVersionDownloadsReportLastUpdateTimeUtc ?? DateTime.UtcNow;
                communityPackageDownloadsReportLastUpdateTimeUtc = communityPackageDownloadsReportLastUpdateTimeUtc ?? DateTime.UtcNow;
                communityPackageVersionDownloadsReportLastUpdateTimeUtc = communityPackageVersionDownloadsReportLastUpdateTimeUtc ?? DateTime.UtcNow;

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentpopularity.json")))
                    .Returns(CreateReport(packageDownloadsReport, packageDownloadsReportLastUpdateTimeUtc.Value));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularity.json")))
                    .Returns(CreateReport(communityPackageDownloadsReport, communityPackageDownloadsReportLastUpdateTimeUtc.Value));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentpopularitydetail.json")))
                    .Returns(CreateReport(packageVersionDownloadsReport, packageVersionDownloadsReportLastUpdateTimeUtc.Value));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularitydetail.json")))
                    .Returns(CreateReport(communityPackageVersionDownloadsReport, communityPackageVersionDownloadsReportLastUpdateTimeUtc.Value));
            }

            private Task<StatisticsReport> CreateReport(IEnumerable<Dictionary<string, object>> report, DateTime lastUpdatedUtc)
            {
                var content = JsonConvert.SerializeObject(report);
                var result = new StatisticsReport(content, lastUpdatedUtc);

                return Task.FromResult(result);
            }
        }
    }
}
