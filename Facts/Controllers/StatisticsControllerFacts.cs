using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using NuGetGallery.Commands;
using NuGetGallery.Statistics;
using Xunit;

namespace NuGetGallery
{
    public class StatisticsControllerFacts
    {
        public class ThePackageDownloadsById
        {

        }

        public class TheIndexAction
        {
            [Fact]
            public async Task ReturnsEmptyReportsIfBothReturnNull()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(null);
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(null);

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageDownloads);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageVersionDownloads);
            }

            [Fact]
            public async Task ReturnsEmptyReportsIfBothThrow()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .Throws(new Exception("ruh roh!"));
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .Throws(new Exception("ruh roh!"));

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageDownloads);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageVersionDownloads);
            }

            [Fact]
            public async Task ReturnsEmptyPackagesReportIfPackagesReturnsNull()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(null);
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(expected);

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageDownloads);
                Assert.Same(expected, model.PackageVersionDownloads);
            }

            [Fact]
            public async Task ReturnsEmptyPackageVersionsReportIfPackageVersionsReturnsNull()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(expected);
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(null);

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageVersionDownloads);
                Assert.Same(expected, model.PackageDownloads);
            }

            [Fact]
            public async Task ReturnsEmptyPackagesReportIfPackagesThrows()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .Throws(new Exception("ruh roh"));
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(expected);

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageDownloads);
                Assert.Same(expected, model.PackageVersionDownloads);
            }

            [Fact]
            public async Task ReturnsEmptyPackageVersionsReportIfPackageVersionsThrows()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(expected);
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .Throws(new Exception("ruh roh"));

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Equal(PackageDownloadsReport.Empty, model.PackageVersionDownloads);
                Assert.Same(expected, model.PackageDownloads);
            }

            [Fact]
            public async Task ReturnsBothReportsIfBothReportsAreLoaded()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected1 = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                var expected2 = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(expected1);
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(expected2);

                // Act
                var result = await controller.Index() as ViewResult;

                // Assert
                Assert.NotNull(result);
                var model = Assert.IsType<StatisticsSummaryViewModel>(result.Model);
                Assert.Same(expected1, model.PackageDownloads);
                Assert.Same(expected2, model.PackageVersionDownloads);
            }
        }
        
        public class ThePackageVersionsAction
        {
            [Fact]
            public async Task ReturnsEmptyReportIfReportFailsToLoad()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(null);

                // Act
                var result = await controller.PackageVersions() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageDownloadsReport.Empty, result.Model);
            }

            [Fact]
            public async Task ReturnsEmptyReportIfReportQueryThrows()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .Throws(new Exception("ruh roh!"));

                // Act
                var result = await controller.PackageVersions() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageDownloadsReport.Empty, result.Model);
            }

            [Fact]
            public async Task ReturnsProvidedReportIfQuerySucceeds()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageVersionDownloads))
                          .CompletesWith(expected);

                // Act
                var result = await controller.PackageVersions() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Same(expected, result.Model);
            }
        }

        public class ThePackagesAction
        {
            [Fact]
            public async Task ReturnsEmptyReportIfReportFailsToLoad()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(null);

                // Act
                var result = await controller.Packages() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageDownloadsReport.Empty, result.Model);
            }

            [Fact]
            public async Task ReturnsEmptyReportIfReportQueryThrows()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .Throws(new Exception("ruh roh!"));

                // Act
                var result = await controller.Packages() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(PackageDownloadsReport.Empty, result.Model);
            }

            [Fact]
            public async Task ReturnsProvidedReportIfQuerySucceeds()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var expected = new PackageDownloadsReport(new[] { new PackageDownloadsReportEntry() });
                controller.OnExecute(new PackageDownloadsReportCommand(ReportNames.RecentPackageDownloads))
                          .CompletesWith(expected);

                // Act
                var result = await controller.Packages() as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Same(expected, result.Model);
            }
        }

        public class TheTotalsAllAction
        {
            [Fact]
            public void UseServerCultureIfLanguageHeadersAreMissing()
            {
                // Arrange
                var currentCulture = CultureInfo.CurrentCulture;

                try
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

                    var controller = Testable.Get<StatisticsController>();
                    var stats = new AggregateStats
                    {
                        Downloads = 2013,
                        TotalPackages = 1000,
                        UniquePackages = 500
                    };
                    controller.OnExecute(new AggregateStatsCommand())
                              .Returns(stats);

                    // Act
                    var result = controller.Totals() as JsonResult;

                    // Asssert
                    Assert.NotNull(result);
                    dynamic data = result.Data;

                    Assert.Equal("2,013", (string)data.Downloads);
                    Assert.Equal("500", (string)data.UniquePackages);
                    Assert.Equal("1,000", (string)data.TotalPackages);
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = currentCulture;
                }
            }

            [Fact]
            public void UseClientCultureIfLanguageHeadersIsPresent()
            {
                // Arrange
                var controller = Testable.Get<StatisticsController>();
                var stats = new AggregateStats
                {
                    Downloads = 2013,
                    TotalPackages = 1000,
                    UniquePackages = 500
                };
                controller.OnExecute(new AggregateStatsCommand())
                          .Returns(stats);
                controller.MockHttpContext()
                          .Setup(c => c.Request.UserLanguages)
                          .Returns(new string[] { "vi-VN" });

                
                // Act
                var result = controller.Totals() as JsonResult;

                // Asssert
                Assert.NotNull(result);
                dynamic data = result.Data;

                Assert.Equal("2.013", (string)data.Downloads);
                Assert.Equal("500", (string)data.UniquePackages);
                Assert.Equal("1.000", (string)data.TotalPackages);
            }
        }
    }
}

