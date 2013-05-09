using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadDetailReportQueryFacts
    {
        public class TheParseReportMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            [InlineData("<xml??>")]
            [InlineData("{not:'the right', format: 42}")]
            [InlineData("{Items:[{ getting: 'there', format: 42}]")]
            [InlineData("{Items:[{ Version: '1.0', Items: [{not: 'quite'}]}]")]
            public void GivenAnInvalidReportString_ItShouldReturnAnEmptyReportObject(string reportContent)
            {
                // Arrange
                var query = new TestablePackageDownloadDetailReportQuery("doesn't matter");
                
                // Act
                var report = query.ParseReport(reportContent);

                // Assert
                Assert.Empty(report);
            }

            [Fact]
            public void GivenAValidReport_ItSetsUpTheReportForPivoting()
            {
                #region Really Big String
                const string reportContent = @"{
                    ""Downloads"": 603,
                    ""Items"": [
                    {
                        ""Version"": ""1.0"",
                        ""Downloads"": 101,
                        ""Items"": [
                        {
                            ""ClientName"": ""NuGet"",
                            ""ClientVersion"": ""2.1"",
                            ""Operation"": ""Install"",
                            ""Downloads"": 101
                        }]
                    }, {
                        ""Version"": ""2.0"",
                        ""Downloads"": 502,
                        ""Items"": [{
                            ""ClientName"": ""NuGet"",
                            ""ClientVersion"": ""2.2"",
                            ""Operation"": ""Update"",
                            ""Downloads"": 201
                        }, {
                            ""ClientName"": ""ProGet"",
                            ""ClientVersion"": ""2.1"",
                            ""Operation"": ""unknown"",
                            ""Downloads"": 301
                        }]
                    }]
                }";
                #endregion

                // Arrange
                var query = new TestablePackageDownloadDetailReportQuery("still doesn't matter");

                // Act
                var report = query.ParseReport(reportContent);

                // Assert
                var expected = new [] {
                    new StatisticsFact(new Dictionary<string, string>() {
                        {"Version", "1.0"},
                        {"ClientName", "NuGet"},
                        {"ClientVersion", "2.1"},
                        {"Operation", "Install"}
                    }, 101),
                    new StatisticsFact(new Dictionary<string, string>() {
                        {"Version", "2.0"},
                        {"ClientName", "NuGet"},
                        {"ClientVersion", "2.2"},
                        {"Operation", "Update"}
                    }, 201),
                    new StatisticsFact(new Dictionary<string, string>() {
                        {"Version", "2.0"},
                        {"ClientName", "ProGet"},
                        {"ClientVersion", "2.1"},
                        {"Operation", "unknown"}
                    }, 301)
                };
                Assert.Equal(expected, report.ToArray());
            }
        }

        public class TestablePackageDownloadDetailReportQuery : PackageDownloadDetailReportQuery
        {
            public TestablePackageDownloadDetailReportQuery(string id) : base(id) { }
            public TestablePackageDownloadDetailReportQuery(string id, string version) : base(id, version) { }

            public IEnumerable<StatisticsFact> ParseReport(string report)
            {
                return ParseReport(new NullDiagnosticsSource(), report);
            }
        }
    }
}
