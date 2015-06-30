// // Copyright (c) .NET Foundation. All rights reserved.
// // Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.WebPages
{
    /// <summary>
    /// This Class tests all of the links on each parent page,
    /// to make sure that there are no broken links on the gallery pages.
    /// </summary>
    public class LinksTests
    {
        private readonly FluentLinkChecker _fluentLinkChecker;

        public LinksTests(ITestOutputHelper testOutputHelper)
        {
            _fluentLinkChecker = new FluentLinkChecker(testOutputHelper);
        }

        [Fact]
        [Priority(1)]
        [Description("Test all clickable links on the gallery's home page are returning 200")]
        [Category("P1Tests")]
        public void TestHomePageLinks()
        {
            _fluentLinkChecker.TestLinksOnWebPage(UrlHelper.BaseUrl);
        }

        [Fact]
        [Priority(1)]
        [Description("Test all clickable links on the Packages page are returning 200")]
        [Category("P1Tests")]
        public void TestPackagesPageLinks()
        {
            _fluentLinkChecker.TestLinksOnWebPage(UrlHelper.PackagesPageUrl);
        }

        [Fact]
        [Priority(1)]
        [Description("Test all clickable links on EntityFramework's package details page are returning 200")]
        [Category("P1Tests")]
        public void TestPackageDetailsPageLinks()
        {
            var pageUrl = UrlHelper.GetPackagePageUrl("EntityFramework", "6.0.0");
            _fluentLinkChecker.TestLinksOnWebPage(pageUrl);
        }

        [Fact]
        [Priority(1)]
        [Description("Test all clickable links on the statistics's home page are returning 200")]
        [Category("P1Tests")]
        public void TestStatisticsPageLinks()
        {
            _fluentLinkChecker.TestLinksOnWebPage(UrlHelper.StatsPageUrl);
        }

        [Fact]
        [Priority(1)]
        [Description("Test all clickable links on the About Gallery page are returning 200")]
        [Category("P1Tests")]
        public void TestAboutGalleryPageLinks()
        {
            _fluentLinkChecker.TestLinksOnWebPage(UrlHelper.AboutGalleryPageUrl);
        }
    }
}