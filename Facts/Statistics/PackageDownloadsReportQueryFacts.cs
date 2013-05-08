using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsReportQueryFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public async Task GivenNoReportExists_ItShouldReturnNull()
            {
                // Arrange
                var mockStorage = new Mock<IFileStorageService>();
                mockStorage.DoesNotContain("stats", "popularity/" + ReportNames.RecentPopularity + ".json");
                var query = new PackageDownloadsReportQuery() { StorageService = mockStorage.Object };
                
                // Act/Assert
                Assert.Null(await query.Execute());
            }

            [Theory]
            [InlineData("")]
            [InlineData("<xml??>")]
            [InlineData("{ 'foo': 42 }")]
            public async Task GivenInvalidReportFiles_ItShouldReturnABlankReport(string fileContent)
            {
                // Arrange
                var mockStorage = new Mock<IFileStorageService>();
                mockStorage.ContainsTextFile("stats", "popularity/" + ReportNames.RecentPopularity + ".json", String.Empty);
                var query = new PackageDownloadsReportQuery() { StorageService = mockStorage.Object };

                // Act/Assert
                Assert.Equal(new PackageDownloadsReport(), await query.Execute());
            }

            [Fact]
            public async Task GivenAValidReport_ItShouldLoadTheReport()
            {
                // Arrange
                const string report = "[{PackageId: 'jQuery', Downloads: '54343'}, {PackageId: 'AjaxControlToolkit', Downloads: '53998'}]";

                var mockStorage = new Mock<IFileStorageService>();
                mockStorage.ContainsTextFile("stats", "popularity/" + ReportNames.RecentPopularity + ".json", report);
                var query = new PackageDownloadsReportQuery() { StorageService = mockStorage.Object };

                // Act/Assert
                Assert.Equal(new PackageDownloadsReport(new [] {
                    new PackageDownloadsReportEntry() { PackageId = "jQuery", Downloads = 54343 },
                    new PackageDownloadsReportEntry() { PackageId = "AjaxControlToolkit", Downloads = 53998 }
                }), await query.Execute());
            }
        }
    }
}
