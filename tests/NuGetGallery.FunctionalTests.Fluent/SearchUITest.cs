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
    public class SearchUITest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify focus scenarios for expanding and contracting search box.")]
        public void SearchUI()
        {
            // Go to the front page.
            I.Open(UrlHelper.BaseUrl);

            // Click in the box
            I.Click("#searchBoxInput", 3, 3);
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width < 200));
            I.Type("fred");
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width > 200));

            // Click out of the box
            I.Click("img[alt='Manage NuGet Packages Dialog Window']", 3, 3);
            I.Expect.True(() => (I.Find("#searchBoxInput")().Width < 200));
        }
    }
}
