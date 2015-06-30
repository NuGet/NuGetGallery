// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.PackageManagement
{

    public class PackageManagementTest : NuGetFluentTest
    {
        public PackageManagementTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Verify basic package management scenarios.")]
        [Priority(2)]
        public async Task PackageManagement()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.PackageManagementTest";

            if (CheckForPackageExistence)
            {
                await UploadPackageIfNecessary(packageName, "1.0.0");
                await UploadPackageIfNecessary(packageName, "2.0.0");
                await UploadPackageIfNecessary(packageName, "3.0.0-rc");
            }

            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // List 1.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/1.0.0/Delete");
            var listedCheckbox = I.Find("#Listed").Element;
            if (listedCheckbox.Attributes.Get("checked") != "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Unlist 2.0.0
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/2.0.0/Delete");
            listedCheckbox = I.Find("#Listed").Element;
            if (listedCheckbox.Attributes.Get("checked") == "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // Unlist 3.0.0-rc
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/3.0.0-rc/Delete");
            listedCheckbox = I.Find("#Listed").Element;
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
            listedCheckbox = I.Find("#Listed").Element;
            if (listedCheckbox.Attributes.Get("checked") == "true")
            {
                I.Click("#Listed");
                I.Wait(1);
            }
            I.Click("input[value='Save']");
            I.Wait(1);

            // List 3.0.0-rc.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName + "/3.0.0-rc/Delete");
            listedCheckbox = I.Find("#Listed").Element;
            if (listedCheckbox.Attributes.Get("checked") != "true")
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
            listedCheckbox = I.Find("#Listed").Element;
            if (listedCheckbox.Attributes.Get("checked") != "true")
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
