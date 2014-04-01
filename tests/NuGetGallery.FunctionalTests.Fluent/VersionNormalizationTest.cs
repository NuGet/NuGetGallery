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
    public class VersionNormalizationTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify normalization of package version numbers.")]
        public void VersionNormalization()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.VersionNormalizationTest";
                        
			UploadPackageIfNecessary(packageName, "0.0.0.0");
			UploadPackageIfNecessary(packageName, "1.0.0.0");
			UploadPackageIfNecessary(packageName, "1.0.0.1");
			UploadPackageIfNecessary(packageName, "1.0.1.0");
			UploadPackageIfNecessary(packageName, "1.0.1.1");
			UploadPackageIfNecessary(packageName, "1.1.0.0");
			UploadPackageIfNecessary(packageName, "1.1.0");
			UploadPackageIfNecessary(packageName, "10.10.10.10");
			UploadPackageIfNecessary(packageName, "20.0.20.0");
            UploadPackageIfNecessary(packageName, "00300.00.0.00300");

            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Expect.Exists("a:contains(' 0.0.0')");
            I.Expect.Exists("a:contains(' 1.0.0')");
            I.Expect.Exists("a:contains(' 1.0.0.1')");
            I.Expect.Exists("a:contains(' 1.0.1')");
            I.Expect.Exists("a:contains(' 1.0.1.1')");
            I.Expect.Exists("a:contains(' 1.1.0')");
            I.Expect.Exists("a:contains(' 10.10.10.10')");
            I.Expect.Exists("a:contains(' 20.0.20')");
            I.Expect.Exists("span:contains(' 300.0.0.300')");
        }
    }
}
