using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGetGallery
{
    public class StatisticsControllerFacts
    {
        [Fact]
        public async void StatisticsHomePage_ValidateReportStructureAndAvailability()
        {
            var fakePackageReport = "[{\"PackageId\":\"A\",\"Downloads\":1},{\"PackageId\":\"B\",\"Downloads\":2}]";
            var fakePackageVersionReport = "[{\"PackageId\":\"A\",\"PackageVersion\":\"1.0\",\"Downloads\":3},{\"PackageId\":\"A\",\"PackageVersion\":\"1.1\",\"Downloads\":4},{\"PackageId\":\"B\",\"PackageVersion\":\"1.0\",\"Downloads\":5}]";

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("RecentPopularity.json")).Returns(Task.FromResult(fakePackageReport));
            fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(fakePackageVersionReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            var model = (StatisticsPackagesViewModel)((ViewResult) await controller.Index()).Model;

            int sum = 0;

            if (model.IsDownloadPackageAvailable)
            {
                foreach (var item in model.DownloadPackagesSummary)
                {
                    sum += item.Downloads;
                }
            }

            if (model.IsDownloadPackageDetailAvailable)
            {
                foreach (var item in model.DownloadPackageVersionsSummary)
                {
                    sum += item.Downloads;
                }
            }

            Assert.Equal<int>(15, sum);
        }

        [Fact]
        public async void StatisticsHomePage_ValidateFullReportStructureAndAvailability()
        {
            var fakePackageReport = "[{\"PackageId\":\"A\",\"Downloads\":1},{\"PackageId\":\"B\",\"Downloads\":2}]";
            var fakePackageVersionReport = "[{\"PackageId\":\"A\",\"PackageVersion\":\"1.0\",\"Downloads\":3},{\"PackageId\":\"A\",\"PackageVersion\":\"1.1\",\"Downloads\":4},{\"PackageId\":\"B\",\"PackageVersion\":\"1.0\",\"Downloads\":5}]";

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("RecentPopularity.json")).Returns(Task.FromResult(fakePackageReport));
            fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(fakePackageVersionReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            var model = (StatisticsPackagesViewModel)((ViewResult) await controller.Index()).Model;

            if (model.IsDownloadPackageAvailable)
            {
                foreach (var item in model.DownloadPackagesSummary)
                {
                    if (item.PackageId == "A" && item.Downloads == 1)
                    {
                        continue;
                    }
                    else if (item.PackageId == "B" && item.Downloads == 2)
                    {
                        continue;
                    }
                    throw new Exception("unexpected data in Package report");
                }
            }

            if (model.IsDownloadPackageDetailAvailable)
            {
                foreach (var item in model.DownloadPackageVersionsSummary)
                {
                    if (item.PackageId == "A" && item.PackageVersion == "1.0" & item.Downloads == 3)
                    {
                        continue;
                    }
                    if (item.PackageId == "A" && item.PackageVersion == "1.1" & item.Downloads == 4)
                    {
                        continue;
                    }
                    if (item.PackageId == "B" && item.PackageVersion == "1.0" & item.Downloads == 5)
                    {
                        continue;
                    }
                    throw new Exception("unexpected data in Package report");
                }
            }
        }

        [Fact]
        public async void StatisticsHomePage_Packages_ValidateReportStructureAndAvailability()
        {
            var fakePackageReport = "[{\"PackageId\":\"A\",\"Downloads\":42},{\"PackageId\":\"B\",\"Downloads\":64}]";

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("RecentPopularity.json")).Returns(Task.FromResult(fakePackageReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            var model = (StatisticsPackagesViewModel)((ViewResult) await controller.Packages()).Model;

            int sum = 0;

            foreach (var item in model.DownloadPackagesAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal<int>(106, sum);
        }

        [Fact]
        public async void StatisticsHomePage_PackageVersions_ValidateReportStructureAndAvailability()
        {
            var fakePackageVersionReport = "[{\"PackageId\":\"A\",\"PackageVersion\":\"1.0\",\"Downloads\":22},{\"PackageId\":\"A\",\"PackageVersion\":\"1.1\",\"Downloads\":20},{\"PackageId\":\"B\",\"PackageVersion\":\"1.0\",\"Downloads\":64}]";

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("RecentPopularityDetail.json")).Returns(Task.FromResult(fakePackageVersionReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            var model = (StatisticsPackagesViewModel)((ViewResult) await controller.PackageVersions()).Model;

            int sum = 0;

            foreach (var item in model.DownloadPackageVersionsAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal<int>(106, sum);
        }

        [Fact]
        public async void StatisticsHomePage_Per_Package_ValidateReportStructureAndAvailability()
        {
            string PackageId = "A";

            JArray client1 = new JArray();
            client1.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.1"), new JProperty("Operation", "Install"), new JProperty("Downloads", 101)));

            JObject item1 = new JObject();
            item1.Add("Version", "1.0");
            item1.Add("Downloads", 101);
            item1.Add("Items", client1);

            JArray client2 = new JArray();
            client2.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.1"), new JProperty("Operation", "Install"), new JProperty("Downloads", 201)));
            client2.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.2"), new JProperty("Operation", "unknown"), new JProperty("Downloads", 301)));

            JObject item2 = new JObject();
            item2.Add("Version", "2.0");
            item2.Add("Downloads", 502);
            item2.Add("Items", client2);

            JArray items = new JArray();
            items.Add(item1);
            items.Add(item2);

            JObject report = new JObject();
            report.Add("Downloads", 603);
            report.Add("Items", items);

            var fakeReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            string reportName = "RecentPopularityDetail_" + PackageId + ".json";
            reportName = reportName.ToLowerInvariant();

            fakeReportService.Setup(x => x.Load(reportName)).Returns(Task.FromResult(fakeReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

            var model = (StatisticsPackagesViewModel)((ViewResult) await controller.PackageDownloadsByVersion(PackageId, new string[] { "Version" })).Model;

            int sum = 0;

            foreach (var row in model.Report.Table)
            {
                sum += int.Parse(row[row.GetLength(0) - 1].Data);
            }

            Assert.Equal<int>(603, sum);
            Assert.Equal<int>(603, model.Report.Total);
        }

        [Fact]
        public async void Statistics_By_Client_Operation_ValidateReportStructureAndAvailability()
        {
            string PackageId = "A";
            string PackageVersion = "2.0";

            JArray client1 = new JArray();
            client1.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.1"), new JProperty("Operation", "Install"), new JProperty("Downloads", 101)));

            JObject item1 = new JObject();
            item1.Add("Version", "1.0");
            item1.Add("Downloads", 101);
            item1.Add("Items", client1);

            JArray client2 = new JArray();
            client2.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.1"), new JProperty("Operation", "Install"), new JProperty("Downloads", 201)));
            client2.Add(new JObject(new JProperty("ClientName", "NuGet"), new JProperty("ClientVersion", "2.2"), new JProperty("Operation", "unknown"), new JProperty("Downloads", 301)));

            JObject item2 = new JObject();
            item2.Add("Version", "2.0");
            item2.Add("Downloads", 502);
            item2.Add("Items", client2);

            JArray items = new JArray();
            items.Add(item1);
            items.Add(item2);

            JObject report = new JObject();
            report.Add("Downloads", 603);
            report.Add("Items", items);

            var fakeReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            string reportName = "RecentPopularityDetail_" + PackageId + ".json";
            reportName = reportName.ToLowerInvariant();

            fakeReportService.Setup(x => x.Load(reportName)).Returns(Task.FromResult(fakeReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://nuget.org"));

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.PackageDownloadsDetail(PackageId, PackageVersion, new string[] { "ClientName" })).Model;

            int sum = 0;

            foreach (var row in model.Report.Table)
            {
                sum += int.Parse(row[row.GetLength(0) - 1].Data);
            }

            Assert.Equal<int>(502, sum);
            Assert.Equal<int>(502, model.Report.Total);
        }

        [Fact]
        public async void StatisticsHomePage_Packages_Negative_ValidateThrowOnInvalidStructure()
        {
            var fakePackageReport = "[{\"Lala\":\"A\",\"Downloads\":303}]";

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("RecentPopularity.json")).Returns(Task.FromResult(fakePackageReport));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object));

            bool hasException = false;

            try
            {
                var model = (StatisticsPackagesViewModel)((ViewResult) await controller.Packages()).Model;
                hasException = false;
            }
            catch (Exception)
            {
                //  we don't care too much about the exact type of the exception
                hasException = true;
            }

            if (!hasException)
            {
                throw new Exception("this exception thrown because expected exception was not thrown");
            }
        }

        public class TheTotalsAllAction
        {
            [Fact]
            public void UseServerCultureIfLanguageHeadersIsMissing()
            {
                // Arrange
                var currentCulture = CultureInfo.CurrentCulture;

                try
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

                    var aggregateStatsService = new Mock<IAggregateStatsService>(MockBehavior.Strict);
                    var stats = new AggregateStats
                    {
                        Downloads = 2013,
                        TotalPackages = 1000,
                        UniquePackages = 500
                    };
                    aggregateStatsService.Setup(s => s.GetAggregateStats()).Returns(stats);

                    var controller = CreateController(aggregateStatsService);

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
                var aggregateStatsService = new Mock<IAggregateStatsService>(MockBehavior.Strict);
                var stats = new AggregateStats
                {
                    Downloads = 2013,
                    TotalPackages = 1000,
                    UniquePackages = 500
                };
                aggregateStatsService.Setup(s => s.GetAggregateStats()).Returns(stats);

                var request = new Mock<HttpRequestBase>();
                request.Setup(r => r.UserLanguages).Returns(new string[] { "vi-VN" });

                var controller = CreateController(aggregateStatsService, request);

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

        public static StatisticsController CreateController(Mock<IAggregateStatsService> aggregateStatsService, Mock<HttpRequestBase> request = null)
        {
            request = request ?? new Mock<HttpRequestBase>();

            var context = new Mock<HttpContextBase>();
            context.SetupGet(s => s.Request).Returns(request.Object);

            var controller = new StatisticsController(aggregateStatsService.Object);
            controller.ControllerContext = new ControllerContext(context.Object, new RouteData(), controller);

            return controller;
        }
    }
}

