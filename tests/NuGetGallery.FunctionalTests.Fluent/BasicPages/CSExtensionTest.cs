// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.BasicPages
{
    public class CSExtensionTest : NuGetFluentTest
    {
        public CSExtensionTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Execute common scenarios with a package whose ID has a '.CS' extension.")]
        [Priority(2)]
        public async Task CSExtension()
        {
            var packageName = "NuGetGallery.FunctionalTests.Fluent.CSExtensionTest.CS";
            var version = "1.0.0";

            await UploadPackageIfNecessary(packageName, version);

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's edit page.
            I.Click("a:contains('Edit Package')");
            I.Expect.Count(1).Of("h2:contains('Editing')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's contact owners page.
            I.Click("a:contains('Contact Owners')");
            I.Expect.Count(1).Of("h1:contains('Contact the Owners of')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's contact support page.
            I.Click("a:contains('Contact Support')");
            I.Expect.Count(1).Of("h1:contains('Contact Support About My Package')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's manage owners page.
            I.Click("a:contains('Manage Owners')");
            I.Expect.Count(1).Of("h1:contains('Manage Owners for Package')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's delete page.
            I.Click("a:contains('Delete Package')");
            I.Expect.Count(1).Of("h2:contains('Why can’t I delete my package?')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's delete page.
            I.Click("a:contains('Package Statistics')");
            I.Expect.Count(1).Of("h1:contains('Package Downloads for')");
            I.Expect.Count(0).Of("h1:contains('404')");

            // Log out
            I.Click("a:contains('Sign out')");
            I.Wait(1);

            // Go back to the package page.
            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);

            // Navigate to the package's report abuse page.
            I.Click("a:contains('Report Abuse')");
            I.Expect.Count(1).Of("h1:contains('Report Abuse')");
            I.Expect.Count(0).Of("h1:contains('404')");
        }
    }
}
