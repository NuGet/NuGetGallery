// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.ComponentModel;

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

        [Fact]
        [Description("Push an invalid package with license expression and verify uploading is blocked")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task UploadInValidPackageWithLicenseExpression()
        {
            // Arrange
            var packageName = $"TestPackageWithLicense.{Guid.NewGuid():N}";
            var packageVersion = "1.0.0";
            
            var licenseExpression = "MIT";
            var expectedErrorMessage = "when a license expression is specified, <licenseUrl> must be set to";

            // Act
            string packageFullPath = await _packageCreationHelper.CreatePackageWithLicenseExpression(packageName, packageVersion, licenseExpression);

            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

            // Assert
            Assert.True(processResult.ExitCode == 1, Constants.UploadFailureMessage);
            Assert.Contains(expectedErrorMessage, processResult.StandardError);
        }

        [Theory]
        [Description("Push an invalid package with license file and verify uploading is blocked")]
        [Priority(1)]
        [Category("P1Tests")]
        [InlineData("license.txt", "license.txt", "It's a license", "when a license file is packaged, <licenseUrl> must be set to ")]
        [InlineData("licensefolder\\license.txt", "license.txt", "It's a license", "does not exist in the package")]
        public async Task UploadInValidPackageWithLicenseFile(string licenseFile, string licenseFileName, string licenseFileContents, string expectedErrorMessage)
        {
            var packageName = $"TestPackageWithLicense.{Guid.NewGuid():N}";
            string packageVersion = "1.0.0";
            string packageFullPath = await _packageCreationHelper.CreatePackageWithLicenseFile(packageName, packageVersion, licenseFile, licenseFileName, licenseFileContents);

            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 1, Constants.UploadFailureMessage);
            Assert.Contains(expectedErrorMessage, processResult.StandardError);
        }
    }
}