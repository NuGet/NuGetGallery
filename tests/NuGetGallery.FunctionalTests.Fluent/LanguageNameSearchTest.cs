using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAutomation;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class LanguageNameSearchTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Validate that the language names C# and C++ return distinct and meaningful results.")]
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
