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
    public class MinClientVersionFromUITest : NuGetFluentTest 
    {

        [TestMethod]
        [Description("Upload a package with a MinClientVersion and validate the min client version number in the package page.")]
        public void MinClientVersionFromUI()
        {
            // Use the same package name, but force the version to be unique.
            string packageName = "NuGetGallery.FunctionalTests.Fluent.MinClientVersionFromUITest";
            string version = "1.0.0";
            UploadPackageIfNecessary(packageName, version, "2.7", packageName, "minclientversion", "A package with a MinClientVersion set for testing purpose only");

            // Validate that the minclientversion is shown to the user on the package page.
            I.Open(UrlHelper.BaseUrl + @"packages/" + packageName + "/" + version);
            string expectedMinClientVersion = @"p:contains('Requires NuGet 2.7 or higher')";

            I.Expect.Count(1).Of(expectedMinClientVersion);
        }
    }
}
