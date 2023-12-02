// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.XunitExtensions;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Commandline
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    public class NugetCommandLineTests
        : GalleryTestBase
    {
        private const string LockedPackageId = "NuGetTest_LockedPackageCannotBeModified";
        private const string LockedPackageVersion = "1.0.0";

        private readonly ClientSdkHelper _clientSdkHelper;
        private readonly CommandlineHelper _commandlineHelper;
        private readonly PackageCreationHelper _packageCreationHelper;

        public NugetCommandLineTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(testOutputHelper);
            _commandlineHelper = new CommandlineHelper(testOutputHelper);
            _packageCreationHelper = new PackageCreationHelper(testOutputHelper);
        }

        [Fact]
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task DownloadPackage()
        {
            string packageId = Constants.TestPackageId; //try to download a pre-defined test package.
            _clientSdkHelper.ClearLocalPackageFolder(packageId, ClientSdkHelper.GetLatestStableVersion(packageId));

            var result = await _commandlineHelper.InstallPackageAsync(packageId, UrlHelper.V2FeedRootUrl, Environment.CurrentDirectory);

            Assert.True(result.ExitCode == 0, Constants.PackageDownloadFailureMessage);
            Assert.True(_clientSdkHelper.CheckIfPackageInstalled(packageId), Constants.PackageInstallFailureMessage);
        }

        public static IEnumerable<object[]> UploadAndUnlistPackages_Data
        {
            get
            {
                yield return new object[] { null };
                yield return new object[] { GalleryConfiguration.Instance.AdminOrganization.ApiKey };
                yield return new object[] { GalleryConfiguration.Instance.CollaboratorOrganization.ApiKey };
            }
        }

        [Theory]
        [MemberData(nameof(UploadAndUnlistPackages_Data))]
        [Description("Tests upload and unlist scenarios with API key")]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task UploadAndUnlistPackages(string apiKey)
        {
            // Can push new package ID
            await _clientSdkHelper.UploadPackage(apiKey);

            // Can push new version of an existing package
            await _clientSdkHelper.UploadPackageVersion(apiKey);

            // Can unlist versions of an existing package
            await _clientSdkHelper.UnlistPackage(apiKey);
        }

        [Fact]
        [Description("Uses scoped API keys to push and unlist packages using Nuget.exe")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task ScopedApiKeysCanOnlyPushAndUnlistWithCorrectScopes()
        {
            // Arrange
            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var commandlineHelper = new CommandlineHelper(TestOutputHelper);

            // Try to upload package using 'unlist' API key
            await _clientSdkHelper.FailToUploadPackage(GalleryConfiguration.Instance.Account.ApiKeyUnlist);

            // Try to upload package using 'push version' API key
            await _clientSdkHelper.FailToUploadPackage(GalleryConfiguration.Instance.Account.ApiKeyPushVersion);

            // Upload package using 'push' API key
            await _clientSdkHelper.UploadPackage(GalleryConfiguration.Instance.Account.ApiKeyPush);

            // Try to upload new version of package using 'unlist' API key
            await _clientSdkHelper.FailToUploadPackageVersion(GalleryConfiguration.Instance.Account.ApiKeyUnlist);

            // Upload new version of package using 'push version' API key
            await _clientSdkHelper.UploadPackageVersion(GalleryConfiguration.Instance.Account.ApiKeyPushVersion);

            // Try unlisting package version1 using 'push' API key
            await _clientSdkHelper.FailToUnlistPackage(GalleryConfiguration.Instance.Account.ApiKeyPush);

            // Try unlisting package version2 using 'push version' API key
            await _clientSdkHelper.FailToUnlistPackage(GalleryConfiguration.Instance.Account.ApiKeyPushVersion);

            // Unlist a package using 'unlist' API key
            await _clientSdkHelper.UnlistPackage(GalleryConfiguration.Instance.Account.ApiKeyUnlist);
        }

        [Fact]
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndDownloadPackageWithMinClientVersion()
        {
            string packageId = $"{Guid.NewGuid():N}PackageWithDotCsNames.Cs";
            string version = "1.0.0";
            string packageFullPath = await _packageCreationHelper.CreatePackageWithMinClientVersion(packageId, version, "2.3");

            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage);

            await _clientSdkHelper.VerifyPackageExistsInV2AndV3Async(packageId, version);

            //Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
            _clientSdkHelper.DownloadPackageAndVerify(packageId, version);
        }

        [PackageLockFact]
        [Description("Verifies push version, and delete are not allowed on a locked package")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task LockedPackageCannotBeModified()
        {
            // Arrange
            string version = "2.0.0";

            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var location = await packageCreationHelper.CreatePackage(LockedPackageId, version);

            // Act & Assert
            // 1. Try to upload package 
            TestOutputHelper.WriteLine($"1. Trying to upload package '{LockedPackageId}', version '{version}' to locked package id.");
            var processResult = await _commandlineHelper.UploadPackageAsync(location, UrlHelper.V2FeedPushSourceUrl);
            Assert.True(processResult.ExitCode != 0, "Package push succeeded, although was expected to fail.");
            Assert.Contains("locked", processResult.StandardError);

            // 2. Try unlisting the locked package 
            // Perform a spot check that the package exists
            await _clientSdkHelper.VerifyPackageExistsInV2Async(LockedPackageId, LockedPackageVersion);
            TestOutputHelper.WriteLine($"5. Trying to unlist locked package '{LockedPackageId}', version '{LockedPackageVersion}'.");
            processResult = await _commandlineHelper.DeletePackageAsync(LockedPackageId, LockedPackageVersion, UrlHelper.V2FeedPushSourceUrl);
            Assert.True(processResult.ExitCode != 0, "Package delete succeeded, although was expected to fail.");
            Assert.Contains("locked", processResult.StandardError);
        }
    }
}
