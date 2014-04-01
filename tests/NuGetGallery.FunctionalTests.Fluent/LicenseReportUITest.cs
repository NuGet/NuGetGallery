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
    public class LicenseReportUITest : NuGetFluentTest
    {
        [TestMethod]
        public void LicenseReportUI()
        {
            // The object here isn't to test Sonatype's detection algorithm, but to validate the UI for a small number of different licenses.
            UploadPackageIfNecessary("NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.MIT", "1.0.0", null, null, null, null, "http://choosealicense.com/licenses/mit/");
            UploadPackageIfNecessary("NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.Apache", "1.0.0", null, null, null, null, "http://choosealicense.com/licenses/apache/");
            UploadPackageIfNecessary("NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.GPL2", "1.0.0", null, null, null, null, "http://choosealicense.com/licenses/gpl-v2/");
            UploadPackageIfNecessary("NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.GPL3", "1.0.0", null, null, null, null, "http://choosealicense.com/licenses/gpl-v3/");
            UploadPackageIfNecessary("NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.NoLicense", "1.0.0", null, null, null, null, null);

            // Navigate to the first package's page. 
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.MIT/1.0.0"));
            // Expect license block on page.
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("p.sonatypeHader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("MIT").In("p.licenseName");
            I.Expect.Count(0).Of("#enableDisableLicenseReportButton");

            // Log on and check again.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.MIT/1.0.0"));
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("#sonatypeHeader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("MIT").In("p.licenseName");
            I.Expect.Value("Diable").In("#enableDisableLicenseReportButton");
            
            // Disable license reports for the package.
            I.Click("#enableDisableLicenseReportButton");
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("#sonatypeHeader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("MIT").In("p.licenseName");
            I.Expect.Value("Enable").In("#enableDisableLicenseReportButton");

            // Log out and validate experience.
            I.Click("a.contains('Sign out')");
            I.Expect.Count(0).Of("div.block");
            I.Expect.Count(0).Of("h3.block-title");
            I.Expect.Count(0).Of("#sonatypeHeader");
            I.Expect.Count(0).Of("a[href='http://sonatype.com/']");
            I.Expect.Count(0).Of("p.licenseName");
            I.Expect.Count(0).Of("#enableDisableLicenseReportButton");

            // Log on and turn reports on again.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.MIT/1.0.0"));
            I.Click("#enableDisableLicenseReportButton");
            I.Expect.Value("Diable").In("#enableDisableLicenseReportButton");

            // Navigate to the second package's page. 
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.Apache/1.0.0"));
            // Expect license block on page.
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("#sonatypeHeader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("Apache").In("p.licenseName");
            I.Expect.Value("Diable").In("#enableDisableLicenseReportButton");

            // Navigate to the third package's page. 
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.GPL2/1.0.0"));
            // Expect license block on page.
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("#sonatypeHader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("GPL-2.0").In("p.licenseName");
            I.Expect.Value("Diable").In("#enableDisableLicenseReportButton");

            // Navigate to the fourth package's page. 
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.GPL3/1.0.0"));
            // Expect license block on page.
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("#sonatypeHeader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("GPL-3.0").In("p.licenseName");
            I.Expect.Value("Diable").In("#enableDisableLicenseReportButton");

            // Navigate to the last package's page. 
            I.Open((UrlHelper.BaseUrl + @"packages/NuGetGallery.FunctionalTests.Fluent.LicenseReportUITest.NoLicense/1.0.0"));
            // Expect license block on page.
            I.Expect.Count(1).Of("div.block");
            I.Expect.Text("License details").In("h3.block-title");
            I.Expect.Text("provided by Sonatype").In("p.sonatypeHeader");
            I.Expect.Count(1).Of("a[href='http://sonatype.com/']");
            I.Expect.Text("License information is not yet available").In("p.block-text");
            I.Expect.Count(0).Of("p.licenseName");
            I.Expect.Count(0).Of("#enableDisableLicenseReportButton");
        }
    }
}
