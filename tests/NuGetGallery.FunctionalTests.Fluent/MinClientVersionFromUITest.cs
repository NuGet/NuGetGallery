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
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string newPackageLocation = PackageCreationHelper.CreatePackage(packageName, version, "2.7");

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // Navigate to the upload page. 
            I.UploadPackageUsingUI(newPackageLocation);

            // Submit on the validate upload page.
            I.Click("input[value='Submit']");

            // Validate that the minclientversion is shown to the user on the package page.
            I.Expect.Url(UrlHelper.BaseUrl + @"packages/" + packageName + "/" + version);
            string expectedMinClientVersion = @"p:contains('Requires NuGet 2.7 or higher')";

            I.Expect.Count(1).Of(expectedMinClientVersion);
        }
    }
}
