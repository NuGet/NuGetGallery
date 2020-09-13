// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using NuGetGallery.Cookies;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class StatisticsControllerFacts : TestContainer
    {
        [Fact]
        public async Task StatisticsHomePage_ValidateReportStructureAndAvailability()
        {
            JArray report1 = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "Downloads", 1 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "Downloads", 2 },
                }
            };

            JArray report2 = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 3 },
                },
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.1" },
                    { "Downloads", 4 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 5 },
                }
            };

            JArray report3 = new JArray
            {
                new JObject
                {
                    { "ClientMajorVersion", 0 },
                    { "ClientMinorVersion", 0 },
                    { "Downloads", 1349 }
                },
                new JObject
                {
                    { "ClientMajorVersion", 1 },
                    { "ClientMinorVersion", 0 },
                    { "Downloads", 1349 }
                }
            };

            JArray report4 = new JArray
            {
                new JObject
                {
                    { "Year", 2012 },
                    { "WeekOfYear", 41 },
                    { "Downloads", 5383767 }
                },
                new JObject
                {
                    { "Year", 2012 },
                    { "WeekOfYear", 42 },
                    { "Downloads", 5383767 }
                }
            };

            var fakePackageReport = report1.ToString();
            var fakePackageVersionReport = report2.ToString();
            var fakeNuGetClientVersion = report3.ToString();
            var fakeLast6Weeks = report4.ToString();

            var fakeReportService = new Mock<IReportService>();

            var updatedUtc = new DateTime(2001, 01, 01, 10, 20, 30);

            fakeReportService.Setup(x => x.Load("recentpopularity.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageReport, DateTime.MinValue)));
            fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, null)));
            fakeReportService.Setup(x => x.Load("recentcommunitypopularity.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageReport, DateTime.MinValue)));
            fakeReportService.Setup(x => x.Load("recentcommunitypopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, null)));

            fakeReportService.Setup(x => x.Load("nugetclientversion.json")).Returns(Task.FromResult(new StatisticsReport(fakeNuGetClientVersion, DateTime.MinValue)));
            fakeReportService.Setup(x => x.Load("last6weeks.json")).Returns(Task.FromResult(new StatisticsReport(fakeLast6Weeks, updatedUtc)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.Index()).Model;

            long sum = 0;

            if (model.IsDownloadPackageAvailable)
            {
                foreach (var item in model.DownloadPackagesSummary)
                {
                    sum += item.Downloads;
                }
            }

            if (model.IsDownloadPackageVersionsAvailable)
            {
                foreach (var item in model.DownloadPackageVersionsSummary)
                {
                    sum += item.Downloads;
                }
            }

            Assert.Equal(15, sum);
            Assert.True(model.LastUpdatedUtc.HasValue);
            Assert.Equal(updatedUtc, model.LastUpdatedUtc.Value);
        }

        [Fact]
        public async Task StatisticsHomePage_ValidateFullReportStructureAndAvailability()
        {
            JArray report1 = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "Downloads", 1 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "Downloads", 2 },
                }
            };

            JArray report2 = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 3 },
                },
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.1" },
                    { "Downloads", 4 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 5 },
                }
            };

            JArray report3 = new JArray
            {
                new JObject
                {
                    { "ClientMajorVersion", 0 },
                    { "ClientMinorVersion", 0 },
                    { "Downloads", 1349 }
                },
                new JObject
                {
                    { "ClientMajorVersion", 1 },
                    { "ClientMinorVersion", 0 },
                    { "Downloads", 1349 }
                }
            };

            JArray report4 = new JArray
            {
                new JObject
                {
                    { "Year", 2012 },
                    { "WeekOfYear", 11 },
                    { "Downloads", 5383767 }
                },
                new JObject
                {
                    { "Year", 2012 },
                    { "WeekOfYear", 12 },
                    { "Downloads", 5383767 }
                }
            };

            var fakePackageReport = report1.ToString();
            var fakePackageVersionReport = report2.ToString();
            var fakeNuGetClientVersion = report3.ToString();
            var fakeLast6Weeks = report4.ToString();

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("recentpopularity.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageReport, DateTime.UtcNow)));
            fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));
            fakeReportService.Setup(x => x.Load("nugetclientversion.json")).Returns(Task.FromResult(new StatisticsReport(fakeNuGetClientVersion, DateTime.UtcNow)));
            fakeReportService.Setup(x => x.Load("last6weeks.json")).Returns(Task.FromResult(new StatisticsReport(fakeLast6Weeks, DateTime.UtcNow)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.Index()).Model;

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

            if (model.IsDownloadPackageVersionsAvailable)
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
        public async Task StatisticsHomePage_Packages_ValidateReportStructureAndAvailability()
        {
            JArray report = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "Downloads", 42 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "Downloads", 64 },
                }
            };

            var fakePackageReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            fakeReportService.Setup(x => x.Load("recentpopularity.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageReport, DateTime.UtcNow)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.Packages()).Model;

            long sum = 0;

            foreach (var item in model.DownloadPackagesAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal(106, sum);
        }

        [Fact]
        public async Task StatisticsHomePage_PackageVersions_ValidateReportStructureAndAvailability()
        {
            JArray report = new JArray
            {
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 22 },
                },
                new JObject
                {
                    { "PackageId", "A" },
                    { "PackageVersion", "1.1" },
                    { "Downloads", 20 },
                },
                new JObject
                {
                    { "PackageId", "B" },
                    { "PackageVersion", "1.0" },
                    { "Downloads", 64 },
                }
            };

            var fakePackageVersionReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();
            var updatedUtc1 = new DateTime(2002, 01, 01, 10, 20, 30);
            var updatedUtc2 = new DateTime(2001, 01, 01, 10, 20, 30);

            fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, updatedUtc1)));
            fakeReportService.Setup(x => x.Load("recentcommunitypopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, updatedUtc2)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.PackageVersions()).Model;

            long sum = 0;

            foreach (var item in model.DownloadPackageVersionsAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal(106, sum);
            Assert.True(model.LastUpdatedUtc.HasValue);
            Assert.Equal(updatedUtc1, model.LastUpdatedUtc.Value);
        }

        [Fact]
        public async void StatisticsHomePage_Per_Package_ValidateModel()
        {
            string PackageId = "A";

            var fakeReportService = new Mock<IReportService>();

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            TestUtility.SetupUrlHelperForUrlGeneration(controller);

            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.PackageDownloadsByVersion(PackageId, new[] { GalleryConstants.StatisticsDimensions.Version })).Model;

            Assert.Equal(PackageId, model.PackageId);
        }

        [Fact]
        public async Task StatisticsHomePage_Per_Package_ValidateReportStructureAndAvailability()
        {
            string PackageId = "A";

            JObject report = new JObject
            {
                { "Downloads", 603 },
                { "Items", new JArray
                    {
                        new JObject
                        {
                            { "Version", "1.0" },
                            { "Downloads", 101 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 101 }
                                    },
                                }
                            }
                        },
                        new JObject
                        {
                            { "Version", "2.0" },
                            { "Downloads", 502 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 201 }
                                    },
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "unknown" },
                                        { "Downloads", 301 }
                                    }
                                }
                            }
                        },
                    }
                }
            };

            var fakeReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            string reportName = "recentpopularity/RecentPopularityDetail_" + PackageId + ".json";
            reportName = reportName.ToLowerInvariant();

            var updatedUtc = new DateTime(2001, 01, 01, 10, 20, 30);
            fakeReportService.Setup(x => x.Load(reportName)).Returns(Task.FromResult(new StatisticsReport(fakeReport, updatedUtc)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            TestUtility.SetupUrlHelperForUrlGeneration(controller);

            var actualReport = (StatisticsPackagesReport)((JsonResult)await controller.PackageDownloadsByVersionReport(PackageId, new[] { GalleryConstants.StatisticsDimensions.Version })).Data;

            int sum = 0;

            foreach (var row in actualReport.Table)
            {
                sum += int.Parse(row[row.GetLength(0) - 1].Data);
            }

            Assert.Equal(603, sum);
            Assert.Equal(603, actualReport.Total);
            Assert.True(actualReport.LastUpdatedUtc.HasValue);
            Assert.Equal(updatedUtc, actualReport.LastUpdatedUtc.Value);
        }

        [Fact]
        public async Task StatisticsHomePage_Per_Package_ValidateReportStructureAndAvailabilityInvalidGroupBy()
        {
            string PackageId = "A";

            JObject report = new JObject
            {
                { "Downloads", 603 },
                { "Items", new JArray
                    {
                        new JObject
                        {
                            { "Version", "1.0" },
                            { "Downloads", 101 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 101 }
                                    },
                                }
                            }
                        },
                        new JObject
                        {
                            { "Version", "2.0" },
                            { "Downloads", 502 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 201 }
                                    },
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "unknown" },
                                        { "Downloads", 301 }
                                    }
                                }
                            }
                        },
                    }
                }
            };

            var fakeReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            string reportName = "recentpopularity/RecentPopularityDetail_" + PackageId + ".json";
            reportName = reportName.ToLowerInvariant();

            var updatedUtc = new DateTime(2001, 01, 01, 10, 20, 30);
            fakeReportService.Setup(x => x.Load(reportName)).Returns(Task.FromResult(new StatisticsReport(fakeReport, updatedUtc)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            TestUtility.SetupUrlHelperForUrlGeneration(controller);

            var invalidDimension = "this_dimension_does_not_exist";
            
            var actualReport = (StatisticsPackagesReport)((JsonResult)await controller.PackageDownloadsByVersionReport(PackageId, new[] { GalleryConstants.StatisticsDimensions.Version, invalidDimension })).Data;

            int sum = 0;

            foreach (var row in actualReport.Table)
            {
                sum += int.Parse(row[row.GetLength(0) - 1].Data);
            }

            Assert.Equal(603, sum);
            Assert.Equal(603, actualReport.Total);
            Assert.True(actualReport.LastUpdatedUtc.HasValue);
            Assert.Equal(updatedUtc, actualReport.LastUpdatedUtc.Value);
            Assert.DoesNotContain(invalidDimension, actualReport.Columns);
        }

        [Fact]
        public async void Statistics_By_Client_Operation_ValidateModel()
        {
            string PackageId = "A";
            string PackageVersion = "2.0";

            var fakeReportService = new Mock<IReportService>();

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            TestUtility.SetupUrlHelperForUrlGeneration(controller);
            
            var model = (StatisticsPackagesViewModel)((ViewResult)await controller.PackageDownloadsDetail(PackageId, PackageVersion, new string[] { "ClientName" })).Model;

            Assert.Equal(PackageId, model.PackageId);
            Assert.Equal(PackageVersion, model.PackageVersion);
        }

        [Fact]
        public async Task Statistics_By_Client_Operation_ValidateReportStructureAndAvailability()
        {
            string PackageId = "A";
            string PackageVersion = "2.0";

            JObject report = new JObject
            {
                { "Downloads", 603 },
                { "Items", new JArray
                    {
                        new JObject
                        {
                            { "Version", "1.0" },
                            { "Downloads", 101 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 101 }
                                    },
                                }
                            }
                        },
                        new JObject
                        {
                            { "Version", "2.0" },
                            { "Downloads", 502 },
                            { "Items", new JArray
                                {
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "Install" },
                                        { "Downloads", 201 }
                                    },
                                    new JObject
                                    {
                                        { "ClientName", "NuGet" },
                                        { "ClientVersion", "2.1" },
                                        { "Operation", "unknown" },
                                        { "Downloads", 301 }
                                    }
                                }
                            }
                        },
                    }
                }
            };

            var fakeReport = report.ToString();

            var fakeReportService = new Mock<IReportService>();

            string reportName = "recentpopularity/RecentPopularityDetail_" + PackageId + ".json";
            reportName = reportName.ToLowerInvariant();

            var updatedUtc = new DateTime(2001, 01, 01, 10, 20, 30);
            fakeReportService.Setup(x => x.Load(reportName)).Returns(Task.FromResult(new StatisticsReport(fakeReport, updatedUtc)));

            var controller = new StatisticsController(new JsonStatisticsService(fakeReportService.Object, new DateTimeProvider()));

            TestUtility.SetupUrlHelperForUrlGeneration(controller);
            
            var actualReport = (StatisticsPackagesReport)((JsonResult)await controller.PackageDownloadsDetailReport(PackageId, PackageVersion, new string[] { "ClientName" })).Data;

            int sum = 0;

            foreach (var row in actualReport.Table)
            {
                sum += int.Parse(row[row.GetLength(0) - 1].Data);
            }

            Assert.Equal(502, sum);
            Assert.Equal(502, actualReport.Total);
            Assert.True(actualReport.LastUpdatedUtc.HasValue);
            Assert.Equal(updatedUtc, actualReport.LastUpdatedUtc.Value);
        }

        [Fact]
        public async Task StatisticsDownloadByVersionAction_ReturnsHttpNotFoundIfPackageDoesntExist()
        {
            const string PackageId = "A";

            var fakeStatisticsService = new Mock<IStatisticsService>();
            fakeStatisticsService
                .Setup(service => service.GetPackageDownloadsByVersion(PackageId))
                .Throws<StatisticsReportNotFoundException>();

            var controller = new StatisticsController(fakeStatisticsService.Object);
            TestUtility.SetupUrlHelperForUrlGeneration(controller);

            await controller.PackageDownloadsByVersionReport(PackageId, It.IsAny<string[]>());

            Assert.Equal(404, controller.Response.StatusCode);
        }

        [Fact]
        public async Task StatisticsDownloadByVersionAction_ReturnsHttpOkIfPackageExists()
        {
            const string PackageId = "A";

            var fakeStatisticsService = new Mock<IStatisticsService>();
            fakeStatisticsService
                .Setup(service => service.GetPackageDownloadsByVersion(PackageId))
                .Returns(Task.FromResult(new StatisticsPackagesReport()));

            var controller = new StatisticsController(fakeStatisticsService.Object);
            TestUtility.SetupUrlHelperForUrlGeneration(controller);

            await controller.PackageDownloadsByVersionReport(PackageId, It.IsAny<string[]>());

            Assert.Equal(200, controller.Response.StatusCode);
        }

        public class TheTotalsAllAction
        {
            [Fact]
            public async Task IgnoresServerCultureAndReturnsStatisticsAsNumbers()
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
                    aggregateStatsService.Setup(s => s.GetAggregateStats()).Returns(Task.FromResult(stats));

                    var controller = CreateController(aggregateStatsService);

                    // Act
                    var result = await controller.Totals() as JsonResult;

                    // Assert
                    Assert.NotNull(result);
                    dynamic data = result.Data;

                    Assert.Equal(2013, data.Downloads);
                    Assert.Equal(500, data.UniquePackages);
                    Assert.Equal(1000, data.TotalPackages);
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = currentCulture;
                }
            }

            [Fact]
            public async Task IgnoresUserCultureAndReturnsStatisticsAsNumbers()
            {
                // Arrange
                var aggregateStatsService = new Mock<IAggregateStatsService>(MockBehavior.Strict);
                var stats = new AggregateStats
                {
                    Downloads = 2013,
                    TotalPackages = 1000,
                    UniquePackages = 500
                };
                aggregateStatsService.Setup(s => s.GetAggregateStats()).Returns(Task.FromResult(stats));

                var request = new Mock<HttpRequestBase>();
                request.Setup(r => r.UserLanguages).Returns(new string[] { "vi-VN" });

                var controller = CreateController(aggregateStatsService, request);
                controller.SetCookieComplianceService(Mock.Of<ICookieComplianceService>());

                // Act

                var result = await InvokeAction(() => (controller.Totals()), controller) as JsonResult;

                // Assert
                Assert.NotNull(result);
                dynamic data = result.Data;

                Assert.Equal(2013, data.Downloads);
                Assert.Equal(500, data.UniquePackages);
                Assert.Equal(1000, data.TotalPackages);
            }

            /// <summary>
            /// When testing MVC controllers, OnActionExecuted and OnActionExecuting functions are not get called.
            /// Code from: http://www.codeproject.com/Articles/623793/OnActionExecuting-and-OnActionExecuted-in-MVC-unit (The Code Project Open License (CPOL))
            /// </summary>
            private async static Task<T> InvokeAction<T>(Expression<Func<Task<T>>> actionCall, Controller controller) where T : ActionResult
            {
                var methodCall = (MethodCallExpression)actionCall.Body;
                var method = methodCall.Method;

                ControllerDescriptor controllerDescriptor = new ReflectedControllerDescriptor(controller.GetType());
                ActionDescriptor actionDescriptor =
                  new ReflectedActionDescriptor(method, method.Name, controllerDescriptor);

                // OnActionExecuting

                var actionExecutingContext = new ActionExecutingContext(controller.ControllerContext,
                  actionDescriptor, new Dictionary<string, object>());
                MethodInfo onActionExecuting = controller.GetType().GetMethod(
                  "OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic);
                onActionExecuting.Invoke(controller, new object[] { actionExecutingContext });

                // call controller method

                T result = await actionCall.Compile()();

                // OnActionExecuted

                var actionExecutedContext = new ActionExecutedContext(controller.ControllerContext,
                  actionDescriptor, false, null) { Result = result };
                MethodInfo onActionExecuted = controller.GetType().GetMethod(
                  "OnActionExecuted", BindingFlags.Instance | BindingFlags.NonPublic);
                onActionExecuted.Invoke(controller, new object[] { actionExecutedContext });

                return (T)actionExecutedContext.Result;
            }
        }

        public static StatisticsController CreateController(Mock<IAggregateStatsService> aggregateStatsService, Mock<HttpRequestBase> request = null)
        {
            request = request ?? new Mock<HttpRequestBase>();

            var context = new Mock<HttpContextBase>();
            context.SetupGet(s => s.Request).Returns(request.Object);
            context.SetupGet(s => s.Items).Returns(new Dictionary<object, object>());

            var controller = new StatisticsController(aggregateStatsService.Object);
            controller.ControllerContext = new ControllerContext(context.Object, new RouteData(), controller);

            return controller;
        }
    }
}