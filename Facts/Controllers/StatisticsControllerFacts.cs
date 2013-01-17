using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class StatisticsControllerFacts
    {
        [Fact]
        public void StatisticsHomePage()
        {
            var fakePackageReport = "[{\"PackageId\":\"A\",\"Downloads\":1},{\"PackageId\":\"B\",\"Downloads\":2}]";
            var fakePackageVersionReport = "[{\"PackageId\":\"A\",\"PackageVersion\":\"1.0\",\"Downloads\":3},{\"PackageId\":\"A\",\"PackageVersion\":\"1.1\",\"Downloads\":4},{\"PackageId\":\"B\",\"PackageVersion\":\"1.0\",\"Downloads\":5}]";

            var fakeStatisticsService = new Mock<IStatisticsService>();

            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularity.json")).Returns(fakePackageReport);
            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularityDetail.json")).Returns(fakePackageVersionReport);

            var controller = new StatisticsController(fakeStatisticsService.Object);

            var model = (StatisticsPackagesViewModel)((ViewResult)controller.Index()).Model;

            int sum = 0;

            if (model.IsDownloadPackageAvailable)
            {
                foreach (var item in model.DownloadPackagesAll)
                {
                    sum += item.Downloads;
                }
            }

            if (model.IsDownloadPackageDetailAvailable)
            {
                foreach (var item in model.DownloadPackageVersionsAll)
                {
                    sum += item.Downloads;
                }
            }

            Assert.Equal<int>(sum, 15);
        }

        [Fact]
        public void StatisticsHomePage_Packages()
        {
            var fakePackageReport = "[{\"PackageId\":\"A\",\"Downloads\":42},{\"PackageId\":\"B\",\"Downloads\":64}]";

            var fakeStatisticsService = new Mock<IStatisticsService>();

            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularity.json")).Returns(fakePackageReport);

            var controller = new StatisticsController(fakeStatisticsService.Object);

            var model = (StatisticsPackagesViewModel)((ViewResult)controller.Packages()).Model;

            int sum = 0;

            foreach (var item in model.DownloadPackagesAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal<int>(sum, 106);
        }

        [Fact]
        public void StatisticsHomePage_PackageVersions()
        {
            var fakePackageVersionReport = "[{\"PackageId\":\"A\",\"PackageVersion\":\"1.0\",\"Downloads\":22},{\"PackageId\":\"A\",\"PackageVersion\":\"1.1\",\"Downloads\":20},{\"PackageId\":\"B\",\"PackageVersion\":\"1.0\",\"Downloads\":64}]";

            var fakeStatisticsService = new Mock<IStatisticsService>();

            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularityDetail.json")).Returns(fakePackageVersionReport);

            var controller = new StatisticsController(fakeStatisticsService.Object);

            var model = (StatisticsPackagesViewModel)((ViewResult)controller.PackageVersions()).Model;

            int sum = 0;

            foreach (var item in model.DownloadPackageVersionsAll)
            {
                sum += item.Downloads;
            }

            Assert.Equal<int>(sum, 106);
        }

        [Fact]
        public void StatisticsHomePage_Per_Package()
        {
            string PackageId = "A";

            var fakeReport = "[{\"PackageVersion\":\"1.0\",\"Downloads\":101},{\"PackageVersion\":\"2.1\",\"Downloads\":202}]";

            var fakeStatisticsService = new Mock<IStatisticsService>();

            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularity_" + PackageId + ".json")).Returns(fakeReport);

            var controller = new StatisticsController(fakeStatisticsService.Object);

            var model = (StatisticsPackagesViewModel)((ViewResult)controller.PackageDownloadsByVersion(PackageId)).Model;

            int sum = 0;

            foreach (var item in model.PackageDownloadsByVersion)
            {
                sum += item.Downloads;
            }

            Assert.Equal<int>(sum, 303);
            Assert.Equal<int>(model.TotalPackageDownloads, 303);
        }

        [Fact]
        public void StatisticsHomePage_Packages_Negative()
        {
            var fakePackageReport = "[{\"Lala\":\"A\",\"Downloads\":303}]";

            var fakeStatisticsService = new Mock<IStatisticsService>();

            fakeStatisticsService.Setup(x => x.LoadReport("RecentPopularity.json")).Returns(fakePackageReport);

            var controller = new StatisticsController(fakeStatisticsService.Object);

            try
            {
                var model = (StatisticsPackagesViewModel)((ViewResult)controller.Packages()).Model;
                throw new Exception("expected exception");
            }
            catch (Exception)
            {
                //  we don't care too much about the exact type of teh exception
            }
        }
    }
}

