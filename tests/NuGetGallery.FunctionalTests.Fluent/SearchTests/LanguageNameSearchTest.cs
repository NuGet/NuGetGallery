using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class LanguageNameSearchTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Validate that the language names C# and C++ return distinct and meaningful results.")]
        [Priority(2)]
        public void LanguageNameSearch()
        {
            // Go to the front page.
            I.Open(UrlHelper.BaseUrl);

            // Search for C++ and C#, verify search results don't cross-contaminate.
            I.Enter("C++").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('C#')");

            I.Enter("C#").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('C++')");
        }
    }
}
