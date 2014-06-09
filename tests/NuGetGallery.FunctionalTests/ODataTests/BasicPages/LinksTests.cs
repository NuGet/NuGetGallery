using FluentLinkChecker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Net;

namespace NuGetGallery.FunctionalTests.LinksTests
{
    /// <summary>
    /// This Class tests all of the links on each parent page, 
    /// to make sure that there are no broken links on the gallery pages. 
    /// </summary>
    [TestClass]
    public class LinksTests
    {
        [TestMethod]
        [Description("Test all clickable links on the gallery's home page are returning 200")]
        [Priority(1)]
        public void TestHomePageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.BaseUrl);
        }

        [TestMethod]
        [Description("Test all clickable links on the Packages page are returning 200")]
        [Priority(1)]
        public void TestPackagesPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.PackagesPageUrl);
        }

        [TestMethod]
        [Description("Test all clickable links on EntityFramework's package details page are returning 200")]
        [Priority(1)]
        public void TestPackageDetailsPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.GetPackagePageUrl("EntityFramework"));
        }

        [TestMethod]
        [Description("Test all clickable links on the statistics's home page are returning 200")]
        [Priority(1)]
        public void TestStatisticsPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.StatsPageUrl);
        }

        [TestMethod]
        [Description("Test all clickable links on the About Gallery page are returning 200")]
        [Priority(1)]
        public void TestAboutGalleryPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.AboutGalleryPageUrl);
        }

        #region Helper Methods
        public bool TestLinksOnWebPagesUsingFluentLinkChecker(string url)
        {
            Console.WriteLine("Starting Url is: {0}", url);
            var result = LinkCheck
                          .On(src => src.Url(new Uri(url))
                          .Relative())
                          .AsBot(bot => bot.Bing())
                          .Start();

            foreach (var link in result)
            {
                Console.WriteLine("Tested Url: {0}, Status Code: {1}", link.Url, link.StatusCode);
                if (link.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }
            }
            // All status codes returned are OK
            return true;
        }
        #endregion
    }
}
