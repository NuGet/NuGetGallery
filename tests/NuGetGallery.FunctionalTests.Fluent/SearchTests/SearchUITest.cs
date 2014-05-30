using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class SearchUITest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify focus scenarios for the search box.")]
        [Priority(2)]
        public void SearchUI()
        {
            // This test made more sense back when our search box expanded and contracted.
            // Now the width of the search box is 650. Update the test, so it verifies the width is large enough.

            // Go to the front page.
            I.Open(UrlHelper.BaseUrl);

            // Click in the box
            I.Click("#searchBoxInput", 3, 3);
            I.Wait(1);
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width > 600));
            I.Type("fred");
            I.Wait(1);
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width > 600));

            // Click out of the box
            I.Click("img[alt='Manage NuGet Packages Dialog Window']", 3, 3);
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width > 600));
            I.Wait(1);
        }
    }
}
