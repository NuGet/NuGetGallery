using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class ErrorPageTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Validate the 404 error page.")]
        [Priority(2)]
        public void ErrorPage()
        {
            // Verify the 500 error page's text.
            // NB:  I'm blocking this for now.  It's clear that the request we're using is a poor proxy for whether the 500 page is served in response to an actual server error.
            // I.Open(UrlHelper.BaseUrl + "/Errors/500?aspxerrorpath=/packages");
            // I.Expect.Count(1).Of("h1:contains('Oh no, we broke something!')");
            // I.Expect.Count(0).Of("h1:contains('Page Not Found')");

            // Verify the 404 error page's text.
            I.Open(UrlHelper.BaseUrl + "/ThisIsNotAMeaningfulUrl");
            I.Expect.Count(0).Of("h1:contains('Oh no, we broke something!')");
            I.Expect.Count(1).Of("h1:contains('Page Not Found')");

            // Search from the 404 page, verify result.
            I.Click("#searchBoxSubmit");
            I.Expect.Url(x => x.AbsoluteUri.Contains("/packages?q=ThisIsNotAMeaningfulUrl"));
        }
    }
}
