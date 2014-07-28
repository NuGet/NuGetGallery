using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class PackageStatsTest : NuGetFluentTest 
    {
        [TestMethod]
        [Description("Toggle checkboxes on a Package's Stats page, verify layout.")]
        [Priority(2)]
        public void PackageStats()
        {
            // We'll use NuGet.Core as the basic test package. 
            I.Open(UrlHelper.BaseUrl + "/stats/packages/jQuery");

            // Verify basic elements of the default page layout, including checkboxes.
            I.Expect.Count(2).Of("label:contains('Version')");  // expect 2, becuase search string also matches "client version"
            I.Expect.Count(1).Of("label:contains('Client Name')");
            I.Expect.Count(1).Of("label:contains('Client Version')");
            I.Expect.Count(1).Of("label:contains('Operation')");
            I.Expect.Count(1).Of("#checkbox-Version");
            I.Expect.Count(1).Of("#checkbox-ClientName");
            I.Expect.Count(1).Of("#checkbox-ClientVersion");
            I.Expect.Count(1).Of("#checkbox-Operation");
            I.Expect.Count(0).Of("th:contains('Version')");
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(0).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Expect.Count(0).Of("td:contains('NuGet Command Line')");
            I.Expect.Count(0).Of("td:contains('Restore')");

            // Sequentially check each box and verify layout.
            I.Click("#checkbox-Version");
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("?groupby=Version"));
            I.Expect.Count(1).Of("th:contains('Version')");
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(1).Of("svg");
            I.Expect.Count(0).Of("td:contains('NuGet Command Line')");
            I.Expect.Count(0).Of("td:contains('Restore')");
            I.Click("#checkbox-Version");  // uncheck the box

            I.Click("#checkbox-ClientName");
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("?groupby=ClientName"));
            I.Expect.Count(0).Of("th:contains('Version')");
            I.Expect.Count(1).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(1).Of("svg");
            I.Expect.Count(1).Of("td:contains('NuGet Command Line')");
            I.Expect.Count(0).Of("td:contains('Restore')");
            I.Click("#checkbox-ClientName");  // uncheck the box

            I.Click("#checkbox-ClientVersion");  
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("?groupby=ClientVersion"));
            I.Expect.Count(1).Of("th:contains('Version')");  // expect 1, because this search string also matches "client version"
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(1).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");  // no graph for this one.
            I.Expect.Count(0).Of("td:contains('NuGet Command Line')");
            I.Expect.Count(0).Of("td:contains('Restore')");
            I.Click("#checkbox-ClientVersion");  // uncheck the box

            I.Click("#checkbox-Operation");
            I.Expect.Url(x => x.AbsoluteUri.EndsWith("?groupby=Operation"));
            I.Expect.Count(0).Of("th:contains('Version')");
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(1).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(1).Of("svg");
            I.Expect.Count(0).Of("td:contains('NuGet Command Line')");
            I.Expect.Count(1).Of("td:contains('Restore')");
            I.Click("#checkbox-Operation"); // uncheck the box

            // Test combinations.
            I.Click("#checkbox-Version");
            I.Click("#checkbox-ClientName");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Version"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientName"));
            I.Expect.Count(1).Of("th:contains('Version')");
            I.Expect.Count(1).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-Version");  // uncheck the box
            I.Click("#checkbox-ClientName");  // uncheck the box

            I.Click("#checkbox-Version");
            I.Click("#checkbox-ClientVersion");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Version"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientVersion"));
            I.Expect.Count(2).Of("th:contains('Version')");  // expect 2, because this is a substring match for "client version" as well
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(1).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-Version");  // uncheck the box
            I.Click("#checkbox-ClientVersion");  // uncheck the box

            I.Click("#checkbox-Version");
            I.Click("#checkbox-Operation");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Version"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Operation"));
            I.Expect.Count(1).Of("th:contains('Version')");
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(1).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-Version");  // uncheck the box
            I.Click("#checkbox-Operation");  // uncheck the box

            I.Click("#checkbox-ClientName");
            I.Click("#checkbox-ClientVersion");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientName"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientVersion"));
            I.Expect.Count(1).Of("th:contains('Version')");  // expect 1, because this is a substring match for "client version"
            I.Expect.Count(1).Of("th:contains('Client Name')");
            I.Expect.Count(1).Of("th:contains('Client Version')");
            I.Expect.Count(0).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-ClientName");  // uncheck the box
            I.Click("#checkbox-ClientVersion");  // uncheck the box

            I.Click("#checkbox-ClientName");
            I.Click("#checkbox-Operation");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientName"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Operation"));
            I.Expect.Count(0).Of("th:contains('Version')");
            I.Expect.Count(1).Of("th:contains('Client Name')");
            I.Expect.Count(0).Of("th:contains('Client Version')");
            I.Expect.Count(1).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-ClientName");  // uncheck the box
            I.Click("#checkbox-Operation");  // uncheck the box

            I.Click("#checkbox-ClientVersion");
            I.Click("#checkbox-Operation");
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=ClientVersion"));
            I.Expect.Url(x => x.AbsoluteUri.Contains("groupby=Operation"));
            I.Expect.Count(1).Of("th:contains('Version')");  // expect 1, because this is a substring match for "client version"
            I.Expect.Count(0).Of("th:contains('Client Name')");
            I.Expect.Count(1).Of("th:contains('Client Version')");
            I.Expect.Count(1).Of("th:contains('Operation')");
            I.Expect.Count(1).Of("th:contains('Downloads')");
            I.Expect.Count(0).Of("svg");
            I.Click("#checkbox-ClientVersion");  // uncheck the box
            I.Click("#checkbox-Operation");  // uncheck the box
        }
    }
}
