using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class PackageManagementTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify basic package management scenarios.")]
        [Priority(2)]
        public void PackageManagement()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.PackageManagementTest";
            FluentAutomation.Element listedCheckbox = null;

            if (CheckForPackageExistence)
            {
                UploadPackageIfNecessary(packageName, "1.0.0");
                UploadPackageIfNecessary(packageName, "2.0.0");
                UploadPackageIfNecessary(packageName, "3.0.0-rc");
            }

            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // List 1.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/1.0.0/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (!(listedCheckbox.Attributes.Get("checked") == "true"))
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Unlist 2.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/2.0.0/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (listedCheckbox.Attributes.Get("checked") == "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Unlist 3.0.0-rc
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/3.0.0-rc/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (listedCheckbox.Attributes.Get("checked") == "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Go to package page, verify shown version is 1.0.0.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Expect.Count(1).Of("h2:contains('1.0.0')");

            // Unlist 1.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/1.0.0/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (listedCheckbox.Attributes.Get("checked") == "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // List 3.0.0-rc.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/3.0.0-rc/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (!(listedCheckbox.Attributes.Get("checked") == "true"))
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Go to package page again, verify shown version is 3.0.0-rc.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Expect.Count(1).Of("h2:contains('3.0.0-rc')");

            // List 1.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/1.0.0/Delete");
            listedCheckbox = I.Find("#Listed").Invoke() as FluentAutomation.Element;
            if (!(listedCheckbox.Attributes.Get("checked") == "true"))
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Go to package page again, verify shown version is 1.0.0.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Expect.Count(1).Of("h2:contains('1.0.0')");
        }
    }
}
