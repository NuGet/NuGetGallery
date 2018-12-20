// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NuGetGallery.FunctionalTests.XunitExtensions;

namespace NuGetGallery.FunctionalTests.License
{
    public class LicenseTests : GalleryTestBase
    {
        private readonly CommandlineHelper _commandlineHelper;
        private readonly PackageCreationHelper _packageCreationHelper;
        public LicenseTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _commandlineHelper = new CommandlineHelper(TestOutputHelper);
            _packageCreationHelper = new PackageCreationHelper(testOutputHelper);
        }

        [Theory]
        [InlineData(null, "MIT", null, null, null, null, "when a license expression is specified, <licenseUrl> must be set to ")]
        [InlineData(null, null, "license.txt", "license.txt", "It's a license", null, "when a license file is packaged, <licenseUrl> must be set to ")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", null, null, null, null, null, "The license deprecation URL must be used in conjunction with specifying the license in the package")]
        [InlineData("https://testNugetLicenseUrl", "MIT", null, null, null, null, "when a license expression is specified, <licenseUrl> must be set to ")]
        [InlineData("https://testNugetLicenseUrl", null, "license.txt", "license.txt", "It's a license", null, "when a license file is packaged, <licenseUrl> must be set to ")]
        [InlineData(null, "MIT", "license.txt", "license.txt", "It's a license", null, "The package manifest contains duplicate metadata elements: 'license'")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", "MIT", "license.txt", "license.txt", "It's a license", null, "The package manifest contains duplicate metadata elements: 'license'")]
        [InlineData("https://testNugetLicenseUrl", "MIT", "license.txt", "license.txt", "It's a license", null, "The package manifest contains duplicate metadata elements: 'license'")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", null, "license", "licenses.txt", "It's a license", null, "does not exist in the package")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", null, "licensefolder\\license.txt", "license.txt", "It's a license", null, "does not exist in the package")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", null, "license.txt", "licensefolder\\license.txt", "It's a license", null, "does not exist in the package")]
        [InlineData("https://aka.ms/deprecateLicenseUrl", null, "license.txt", "license.txt", null, new byte[] { 1,2,3,4,5}, "The license file must be plain text using UTF-8 encoding")]
        public async Task UploadValidLicensePackage(string licenseUrl, string licenseExpression, string licenseFile, string licenseFileName, string licenseFileContents, byte[] licenseBinaryFileContents, string expectedErrorMessage)
        {
            var packageName = $"TestPackageWithLicense.{DateTime.UtcNow.Ticks}";
            string packageVersion = "1.0.0";
            string packageFullPath = await _packageCreationHelper.CreatePackageWithLicense(packageName, packageVersion, licenseUrl, licenseExpression, licenseFile, licenseFileName, licenseFileContents, licenseBinaryFileContents);

            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 1, Constants.UploadFailureMessage);
            Assert.Contains(expectedErrorMessage, processResult.StandardError);
        }
    }
}