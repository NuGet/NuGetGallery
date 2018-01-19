﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.UploadAndDownload
{
    public class _1826RegressionTest : NuGetFluentTest
    {
        private readonly PackageCreationHelper _packageCreationHelper;

        public _1826RegressionTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _packageCreationHelper = new PackageCreationHelper(testOutputHelper);
        }

        [Fact]
        [Description("Upload a package with a dependency that has no targetFramework, verify success.")]
        [Priority(1)]
        public async Task _1826Regression()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent._1826RegressionTest";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

            string newPackageLocation = await _packageCreationHelper.CreatePackage(packageName, version, null, null, null, null, null, @"
                <group>
                    <dependency id=""jQuery"" version=""2.1.0"" />
                </group>
                <group targetFramework="".NETFramework4.0"">
                    <dependency id=""Newtonsoft.Json"" version=""6.0.1"" />
                    <dependency id=""jQuery"" version=""2.1.0"" />
                </group>
            ");

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountEmail, EnvironmentSettings.TestAccountPassword);

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
