using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

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
                Assert.True(_target.DownloadPackagesResult.Loaded);
                Assert.True(_target.DownloadPackageVersionsResult.Loaded);
                Assert.True(_target.DownloadCommunityPackagesResult.Loaded);
                Assert.True(_target.DownloadCommunityPackageVersionsResult.Loaded);

                Assert.NotNull(_target.DownloadPackagesResult.LastUpdatedUtc);
                Assert.NotNull(_target.DownloadPackageVersionsResult.LastUpdatedUtc);
                Assert.NotNull(_target.DownloadCommunityPackagesResult.LastUpdatedUtc);
                Assert.NotNull(_target.DownloadCommunityPackageVersionsResult.LastUpdatedUtc);

                Assert.Equal(LastUpdatedUtcDefault, _target.DownloadPackagesResult.LastUpdatedUtc);
                Assert.Equal(LastUpdatedUtcDefault, _target.DownloadPackageVersionsResult.LastUpdatedUtc);
                Assert.Equal(LastUpdatedUtcDefault, _target.DownloadCommunityPackagesResult.LastUpdatedUtc);
                Assert.Equal(LastUpdatedUtcDefault, _target.DownloadCommunityPackageVersionsResult.LastUpdatedUtc);

                Assert.Equal(2, _target.DownloadPackagesAll.Count());
                Assert.Equal(3, _target.DownloadPackageVersionsAll.Count());
                Assert.Equal(2, _target.DownloadCommunityPackagesAll.Count());
                Assert.Equal(3, _target.DownloadCommunityPackageVersionsAll.Count());

                VerifyReportsLoadedOnce();
            }

            [Fact]
            public async Task DoesntReloadReportsIfCached()
            {
                // Arrange
                Mock();

                // Act
                await _target.Refresh();
                await _target.Refresh();

                // Assert - The reports should only be loaded once due to caching.
                VerifyReportsLoadedOnce();
            }

            [Fact]
            public async Task LastUpdatedIsGreatestNonNullReportUpdateTime()
            {
                // Arrange
                Mock(
                    packagesLastUpdateTimeUtc: LastUpdatedUtcDefault,
                    packageVersionsLastUpdateTimeUtc: LastUpdatedUtcDefault.AddHours(1),
                    communityPackagesLastUpdateTimeUtc: null,
                    communityPackageVersionsLastUpdateTimeUtc: LastUpdatedUtcDefault.AddHours(-1));

                // Act
                await _target.Refresh();

                // Assert
                Assert.Equal(LastUpdatedUtcDefault.AddHours(1), _target.LastUpdatedUtc);
            }

            [Fact]
            public async Task LoadReportsFailsIfDownloadsIsMissingOrNotInteger()
            {
                // Arrange
                var packagesReport = PackageDownloadsReport;
                var packageVersionsReport = PackageVersionDownloadsReport;
                var communityPackagesReport = PackageDownloadsReport;
                var communityPackageVersionsReport = PackageVersionDownloadsReport;

                packagesReport[0].Remove("Downloads");
                packageVersionsReport[0].Remove("Downloads");
                communityPackagesReport[1]["Downloads"] = "A lot of downloads";
                communityPackageVersionsReport[1]["Downloads"] = "A lot of downloads";

                Mock(
                    packageDownloadsReport: packagesReport,
                    packageVersionDownloadsReport: packageVersionsReport,
                    communityPackageDownloadsReport: communityPackagesReport,
                    communityPackageVersionDownloadsReport: communityPackageVersionsReport);

                // Act
                await _target.Refresh();

                // Assert
                Assert.False(_target.DownloadPackagesResult.Loaded);
                Assert.False(_target.DownloadPackageVersionsResult.Loaded);
                Assert.False(_target.DownloadCommunityPackagesResult.Loaded);
                Assert.False(_target.DownloadCommunityPackageVersionsResult.Loaded);

                VerifyReportsLoadedOnce();
            }
        }

        public class FactsBase
        {
            public readonly DateTime LastUpdatedUtcDefault = DateTime.UtcNow;

            public readonly string Package1Id = "A.Fantastic.Package";
            public readonly string Package1Version1 = "1.0.0";
            public readonly string Package1Version2 = "2.0.0";
            public readonly int Package1Version1Downloads = 123;
            public readonly int Package1Version2Downloads = 456;

            public readonly string Package2Id = "Foo.Bar";
            public readonly string Package2Version = "1.0.0";
            public readonly int Package2Downloads = 789;

            protected readonly Mock<IReportService> _reportService;
            protected readonly JsonStatisticsService _target;

            protected Dictionary<string, object>[] PackageDownloadsReport => new[]
            {
                new Dictionary<string, object>()
                {
                    { "PackageId", Package1Id },
                    { "Downloads", Package1Version1Downloads + Package1Version2Downloads },
                },

                new Dictionary<string, object>()
                {
                    { "PackageId", Package2Id },
                    { "Downloads", Package2Downloads },
                },
            };

            protected Dictionary<string, object>[] PackageVersionDownloadsReport => new[]
            {
                new Dictionary<string, object>()
                {
                    { "PackageId", Package1Id },
                    { "PackageVersion", Package1Version1 },
                    { "Downloads", Package1Version1Downloads },
                },

                new Dictionary<string, object>()
                {
                    { "PackageId", Package1Id },
                    { "PackageVersion", Package1Version2 },
                    { "Downloads", Package1Version2Downloads },
                },

                new Dictionary<string, object>()
                {
                    { "PackageId", Package2Id },
                    { "PackageVersion", Package2Version },
                    { "Downloads", Package2Downloads },
                },
            };

            public FactsBase()
            {
                _reportService = new Mock<IReportService>();

                _target = new JsonStatisticsService(_reportService.Object);
            }

            protected void Mock(
                IEnumerable<Dictionary<string, object>> packageDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> packageVersionDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> communityPackageDownloadsReport = null,
                IEnumerable<Dictionary<string, object>> communityPackageVersionDownloadsReport = null,

                DateTime? packagesLastUpdateTimeUtc = null,
                DateTime? packageVersionsLastUpdateTimeUtc = null,
                DateTime? communityPackagesLastUpdateTimeUtc = null,
                DateTime? communityPackageVersionsLastUpdateTimeUtc = null)
            {
                packageDownloadsReport = packageDownloadsReport ?? PackageDownloadsReport;
                packageVersionDownloadsReport = packageVersionDownloadsReport ?? PackageVersionDownloadsReport;
                communityPackageDownloadsReport = communityPackageDownloadsReport ?? PackageDownloadsReport;
                communityPackageVersionDownloadsReport = communityPackageVersionDownloadsReport ?? PackageVersionDownloadsReport;

                // Set the default last updated timestamps only if all timestamps are null.
                if (!packagesLastUpdateTimeUtc.HasValue && !packageVersionsLastUpdateTimeUtc.HasValue &&
                    !communityPackagesLastUpdateTimeUtc.HasValue && !communityPackageVersionsLastUpdateTimeUtc.HasValue)
                {
                    packagesLastUpdateTimeUtc = packagesLastUpdateTimeUtc ?? LastUpdatedUtcDefault;
                    packageVersionsLastUpdateTimeUtc = packageVersionsLastUpdateTimeUtc ?? LastUpdatedUtcDefault;
                    communityPackagesLastUpdateTimeUtc = communityPackagesLastUpdateTimeUtc ?? LastUpdatedUtcDefault;
                    communityPackageVersionsLastUpdateTimeUtc = communityPackageVersionsLastUpdateTimeUtc ?? LastUpdatedUtcDefault;
                }

                Task<StatisticsReport> CreateReport(IEnumerable<Dictionary<string, object>> report, DateTime? lastUpdatedUtc)
                {
                    var content = JsonConvert.SerializeObject(report);
                    var result = new StatisticsReport(content, lastUpdatedUtc);

                    return Task.FromResult(result);
                }

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentpopularity.json")))
                    .Returns(CreateReport(packageDownloadsReport, packagesLastUpdateTimeUtc));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularity.json")))
                    .Returns(CreateReport(communityPackageDownloadsReport, communityPackagesLastUpdateTimeUtc));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentpopularitydetail.json")))
                    .Returns(CreateReport(packageVersionDownloadsReport, packageVersionsLastUpdateTimeUtc));

                _reportService
                    .Setup(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularitydetail.json")))
                    .Returns(CreateReport(communityPackageVersionDownloadsReport, communityPackageVersionsLastUpdateTimeUtc));
            }

            protected void VerifyReportsLoadedOnce()
            {
                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentpopularity.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularity.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentpopularitydetail.json")), Times.Once);

                _reportService
                    .Verify(s => s.Load(It.Is<string>(n => n == "recentcommunitypopularitydetail.json")), Times.Once);
            }
        }
    }
}
