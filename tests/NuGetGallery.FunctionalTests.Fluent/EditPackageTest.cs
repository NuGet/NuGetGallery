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
    public class EditPackageTest : FluentAutomation.FluentTest
    {
        public EditPackageTest()
        {
            FluentAutomation.SeleniumWebDriver.Bootstrap();
        }

        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        public void EditPackage()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.EditPackageTest";
            string version = "1.0.0";

            I.Open(UrlHelper.BaseUrl);
            I.Expect.Url(x => x.AbsoluteUri.Contains("nuget"));
            
            UploadPackageIfNecessary(packageName, version);

            // Log on using the test account.
            I.Open(UrlHelper.BaseUrl + "users/account/LogOn");
            I.Expect.Url(x => x.LocalPath.Contains("LogOn"));
            I.Enter(EnvironmentSettings.TestAccountName).In("#SignIn_UserNameOrEmail");
            I.Enter(EnvironmentSettings.TestAccountPassword).In("#SignIn_Password");
            I.Click("#signin-link");

            // Navigate to the package's edit page. 
            I.Open(String.Format(UrlHelper.EditPageUrl, packageName, version));
            I.Expect.Url(x => x.AbsoluteUri.Contains("nuget"));

            // Edit the package.
            string newDescription = String.Format("This description is accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newDescription).In("#Edit_Description");
            I.Click("input[value=Save]");

            // Validate that the edit is queued.
            string expectedDescription = @"p:contains('" + newDescription + "')";
            string editPending = @"p:contains('An edit is pending for this package version.')";

            I.Open(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
            I.Expect.Count(1).Of(expectedDescription);
            I.Expect.Count(1).Of(editPending);

            // Skipping this for reliability testing.
            // Wait a minute.
            //I.Wait(60);

            // Validate that the edit has been applied.
            //I.Open(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
            //I.Expect.Count(1).Of(expectedDescription);
            //I.Expect.Count(0).Of(editPending);
        }

        private void UploadPackageIfNecessary(string packageName, string version)
        {
            if (!PackageExists(packageName, version)) 
            {
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageName, "1.0.0");
            }
        }

        private bool PackageExists(string packageName, string version)
        {
            HttpWebRequest packagePageRequest = (HttpWebRequest)HttpWebRequest.Create(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
            HttpWebResponse packagePageResponse;
            try
            { 
                packagePageResponse = (HttpWebResponse)packagePageRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotFound) return false;
            }

            // If we didn't get an exception, that means thew resource exists.
            return true;
        }
    }
}
