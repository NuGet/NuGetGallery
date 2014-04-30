using FluentLinkChecker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web.UI;

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
        public void TestHomePageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.BaseUrl);
        }

        [TestMethod]
        public void TestPackagesPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.PackagesPageUrl);
        }

        [TestMethod]
        public void TestPackageDetailsPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.GetPackagePageUrl("EntityFramework"));
        }

        [TestMethod]
        public void TestStatisticsPageLinks()
        {
            TestLinksOnWebPagesUsingFluentLinkChecker(UrlHelper.StatsPageUrl);
        }

        #region Helper Methods
        public bool TestLinksOnWebPagesUsingFluentLinkChecker(string uri)
        {
            var result = LinkCheck
                          .On(src => src.Url(new Uri(uri))
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
