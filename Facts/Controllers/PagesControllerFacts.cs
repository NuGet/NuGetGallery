using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class PagesControllerFacts
    {
        public class TheStatsAction
        {
            [Fact]
            public void UseServerCultureIfLanguageHeadersIsMissing()
            {
                // Arrange
                var currentCulture = CultureInfo.CurrentCulture;

                try
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

                    var statsService = new Mock<IAggregateStatsService>(MockBehavior.Strict);
                    var stats = new AggregateStats
                    {
                        Downloads = 2013,
                        TotalPackages = 1000,
                        UniquePackages = 500
                    };
                    statsService.Setup(s => s.GetAggregateStats()).Returns(stats);

                    var controller = CreateController(statsService);

                    // Act
                    var result = controller.Stats() as JsonResult;

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
                var statsService = new Mock<IAggregateStatsService>(MockBehavior.Strict);
                var stats = new AggregateStats
                {
                    Downloads = 2013,
                    TotalPackages = 1000,
                    UniquePackages = 500
                };
                statsService.Setup(s => s.GetAggregateStats()).Returns(stats);

                var request = new Mock<HttpRequestBase>();
                request.Setup(r => r.UserLanguages).Returns(new string[] { "vi-VN" });

                var controller = CreateController(statsService, request);

                // Act
                var result = controller.Stats() as JsonResult;

                // Asssert
                Assert.NotNull(result);
                dynamic data = result.Data;

                Assert.Equal("2.013", (string)data.Downloads);
                Assert.Equal("500", (string)data.UniquePackages);
                Assert.Equal("1.000", (string)data.TotalPackages);
            }
        }

        public static PagesController CreateController(Mock<IAggregateStatsService> statsService, Mock<HttpRequestBase> request = null) 
        {
            request = request ?? new Mock<HttpRequestBase>();

            var context = new Mock<HttpContextBase>();
            context.SetupGet(s => s.Request).Returns(request.Object);

            var controller = new PagesController(statsService.Object);
            controller.ControllerContext = new ControllerContext(context.Object, new RouteData(), controller);

            return controller;
        }

    }
}
