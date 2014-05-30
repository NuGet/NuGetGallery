using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class SearchSyntaxTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Provide sanity testing of Nuget site search syntax.")]
        [Priority(2)]
        public void SearchSyntax()
        {
            // These words are obscure and unlikely to be used by another package.  
            // Change them if this is no longer the case.
            string word1 = "versichern";
            string word2 = "vraisemblance";
            string word3 = "withes";
            string word4 = "sortira";
            string word5 = "solis";
            string word6 = "unshod";
            string word7 = "trottoirs";
            string word8 = "teintes";

            // Create two packages with the same keywords in different fields.
            string packageName1 = word1 + "." + word2;
            string title1 = word3;
            string tags1 = word4;
            string description1 = word5 + " " + word6 + " " + word2;
            string packageName2 = word5 + "." + word1;
            string title2 = word2;
            string tags2 = word3;
            string description2 = word4 + " " + word7 + " " + word2;

            UploadPackageIfNecessary(packageName1, "1.0.0", null, title1, tags1, description1);
            UploadPackageIfNecessary(packageName2, "1.0.0", null, title2, tags2, description2);

            // Go to the front page.
            I.Open(UrlHelper.BaseUrl);

            // 1.  Generic search for a keyword.  Should match both packages.
            I.Enter(word1).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 2')");

            // 2.  Single keyword.  Should match no package.
            I.Enter(word8).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");

            // 3.  Multiple keywords.  Should match both packages.
            I.Enter(word1 + " " + word6).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 2')");

            // 4.  Quotes.  Should match one package based on description.
            I.Enter("\"" + word4 + " " + word7 + "\"").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 5.  Quotes.  Should match one package based on title.
            I.Enter("\"" + word1 + " " + word2 + "\"").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            // I.Expect.Count(0).Of("h1:contains('" + title2 + "')"); -- Not checked since the title will be found in the search string.
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 6.  Quotes.  Should match no package.
            I.Enter("\"" + word1 + " " + word3 + "\"").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            // I.Expect.Count(0).Of("h1:contains('" + title1 + "')"); -- Not checked since the title will be found in the search string.
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");

            // 7.  ID field.  Should match 2 packages.
            I.Enter("id:" + word1).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 2')");

            // 8.  ID field.  Should match 1 package.
            I.Enter("ID:" + word5).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 9.  ID filter.  Should match no packages.
            I.Enter("Id:" + word3).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            // I.Expect.Count(0).Of("h1:contains('" + title1 + "')"); -- Not checked since the title will be found in the search string.
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");

            // 10. ID filter with quotes.  Should match 1 package.
            I.Enter("Id:\"" + word5 + "." + word1 + "\"").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            /* Starting API V3, searching using Title: and description: is no longer supported. So commenting out the tests here.
            // 11. Description filter.  Should match one package.
            I.Enter("description:" + word5).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 12. Description filter.  Should match no package.
            I.Enter("description:" + word3).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            // I.Expect.Count(0).Of("h1:contains('" + title1 + "')"); -- Not checked since the title will be found in the search string.
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");

            // 13. Multiple Description filters.  Should match one package.
            I.Enter("description:" + word7 + " description:" + word4).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(1).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 14. Description filter with quotes.  Should match one package.
            I.Enter("description:\"" + word6 + " " + word2 + "\"").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            // I.Expect.Count(0).Of("h1:contains('" + title2 + "')"); -- Not checked since the title will be found in the search string.
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 15. Title filter.  Should match one package.
            I.Enter("title:" + word2).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(2).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 16. Title filter.  Should match no package.
            I.Enter("title:" + word1).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");
             * */

            // 17. Tags filter.  Should match one package.
            I.Enter("tags:" + word4).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(1).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 1')");

            // 18. Tags filter.  Should match no package.
            I.Enter("tags:" + word6).In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('" + title1 + "')");
            I.Expect.Count(0).Of("h1:contains('" + title2 + "')");
            I.Expect.Count(1).Of("h1:contains('returned 0')");
        }
    }
}
