using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class _1826RegressionTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Upload a package with a dependency that has no targetFramework, verify success.")]
        [Priority(1)]
        public void _1826Regression()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent._1826RegressionTest";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

            string newPackageLocation = PackageCreationHelper.CreatePackage(packageName, version, null, null, null, null, null, @"
                <group>
                    <dependency id=""jQuery"" version=""2.1.0"" />
                </group>
                <group targetFramework="".NETFramework4.0"">
                    <dependency id=""Newtonsoft.Json"" version=""6.0.1"" />
                    <dependency id=""jQuery"" version=""2.1.0"" />
                </group>
            ");
            
            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // Navigate to the upload page and upload the package. 
            I.UploadPackageUsingUI(newPackageLocation);
            I.Click("#verifyUploadSubmit");

            // Validate that the package has uploaded.
            I.Expect.Url(UrlHelper.BaseUrl + @"packages/" + packageName + "/" + version);
            I.Expect.Count(1).Of("h4:contains('All Frameworks')");
            I.Expect.Count(1).Of("h4:contains('.NETFramework 4.0')");
        }
    }
}
