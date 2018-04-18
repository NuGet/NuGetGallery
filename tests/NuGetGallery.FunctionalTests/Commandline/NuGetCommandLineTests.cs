// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
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

        [Fact]
        [Description("Tests upload and unlist scenarios as self")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndUnlistPackagesAsSelf()
        {
            // Can push new package ID as self
            var id = UploadHelper.GetUniquePackageId(nameof(UploadAndUnlistPackagesAsSelf));
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "1.0.0");

            // Can push new version of an existing package as self
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "2.0.0");

            // Can unlist versions of an existing package as self
            await _clientSdkHelper.UnlistPackageAndVerify(id, "2.0.0");
        }

        [Fact]
        [Description("Tests upload and unlist scenarios as an organization admin")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndUnlistPackagesAsOrganizationAdmin()
        {
            var apiKey = EnvironmentSettings.TestOrganizationAdminAccountApiKey;

            // Can push new package ID as organization
            var id = UploadHelper.GetUniquePackageId(nameof(UploadAndUnlistPackagesAsOrganizationAdmin));
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "1.0.0", apiKey: apiKey);

            // Can push new version of an existing package as organization
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "2.0.0", apiKey: apiKey);

            // Can unlist versions of an existing package as organization
            await _clientSdkHelper.UnlistPackageAndVerify(id, "2.0.0", apiKey);
        }

        [Fact]
        [Description("Tests upload and unlist scenarios as an organization collaborator")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndUnlistPackagesAsOrganizationCollaborator()
        {
            var apiKey = EnvironmentSettings.TestOrganizationCollaboratorAccountApiKey;

            // Can push new package ID as organization
            var id = UploadHelper.GetUniquePackageId(nameof(UploadAndUnlistPackagesAsOrganizationCollaborator));
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "1.0.0", apiKey: apiKey);

            // Can push new version of an existing package as organization
            await _clientSdkHelper.UploadNewPackageAndVerify(id, "2.0.0", apiKey: apiKey);

            // Can unlist versions of an existing package as organization
            await _clientSdkHelper.UnlistPackageAndVerify(id, "2.0.0", apiKey);
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

            var packageId = UploadHelper.GetUniquePackageId(nameof(ScopedApiKeysCanOnlyPushAndUnlistWithCorrectScopes));
            var version1 = "1.0.0";
            var version2= "2.0.0";

            // 1. Try to upload package using 'unlist' api key => expect failure
            TestOutputHelper.WriteLine($"1. Trying to upload package '{packageId}', version '{version1}' using 'unlist' API key. Expected result: failure.");
            await _clientSdkHelper.UploadNewPackage(packageId, version1, apiKey: EnvironmentSettings.TestAccountApiKey_Unlist, success: false);

            // 2. Try to upload package using 'push version' api key => expect failure
            TestOutputHelper.WriteLine($"2. Trying to upload package '{packageId}', version '{version1}' using 'push version' API key. Expected result: failure.");
            await _clientSdkHelper.UploadNewPackage(packageId, version1, apiKey: EnvironmentSettings.TestAccountApiKey_PushVersion, success: false);

            // 3. Upload package using 'push' api key => expect success
            TestOutputHelper.WriteLine($"3. Trying to upload package '{packageId}', version '{version1}' using 'push' API key. Expected result: success.");
            await _clientSdkHelper.UploadNewPackage(packageId, version1, apiKey: EnvironmentSettings.TestAccountApiKey_Push);

            // 4. Upload new version of package using 'push version' api key => expect success
            TestOutputHelper.WriteLine($"4. Trying to upload package '{packageId}', version '{version2}' using 'push version' API key. Expected result: success.");
            await _clientSdkHelper.UploadNewPackage(packageId, version2, apiKey: EnvironmentSettings.TestAccountApiKey_PushVersion);

            // Verify the existence of the two pushed packages.
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version1);
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version2);

            // 5. Try unlisting package version1 using 'push' api key => expect failure
            TestOutputHelper.WriteLine($"5. Trying to unlist package '{packageId}', version '{version1}' using 'push' API key. Expected result: failure.");
            await _clientSdkHelper.UnlistPackage(packageId, version1, EnvironmentSettings.TestAccountApiKey_Push, success: false);

            // 6. Try unlisting package version2 using 'push version' api key => expect failure
            TestOutputHelper.WriteLine($"6. Trying to unlist package '{packageId}', version '{version2}' using 'push' API key. Expected result: failure.");
            await _clientSdkHelper.UnlistPackage(packageId, version2, EnvironmentSettings.TestAccountApiKey_PushVersion, success: false);

            // 7. Unlist both packages using 'unlist' api key => expect succees
            TestOutputHelper.WriteLine($"7. Trying to unlist package '{packageId}', version '{version1}' using 'unlist' API key. Expected result: success.");
            await _clientSdkHelper.UnlistPackage(packageId, version1, EnvironmentSettings.TestAccountApiKey_Unlist);

            TestOutputHelper.WriteLine($"8. Trying to unlist package '{packageId}', version '{version2}' using 'unlist' API key. Expected result: success.");
            await _clientSdkHelper.UnlistPackage(packageId, version2, EnvironmentSettings.TestAccountApiKey_Unlist);
        }

        [Fact]
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndDownloadPackageWithMinClientVersion()
        {
            string packageId = DateTime.Now.Ticks + "PackageWithDotCsNames.Cs";
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
            _clientSdkHelper.DownloadPackageAndVerify(packageId);
        }

        [PackageLockFact]
        [Description("Verifies push version, and delete are not allowed on a locked package")]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LockedPackageCannotBeModified()
        {
            // Arrange
            string version = "2.0.0";

            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var location = await packageCreationHelper.CreatePackage(LockedPackageId, version);

            // Act & Assert
            // 1. Try to upload package 
            TestOutputHelper.WriteLine($"1. Trying to upload package '{LockedPackageId}', version '{version}' to locked package id.");
            var processResult = await _commandlineHelper.UploadPackageAsync(location, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey);
            Assert.True(processResult.ExitCode != 0, "Package push succeeded, although was expected to fail.");
            Assert.Contains("locked", processResult.StandardError);

            // 2. Try unlisting the locked package 
            // Perform a sanity check that the package exists
            await _clientSdkHelper.VerifyPackageExistsInV2AndV3Async(LockedPackageId, LockedPackageVersion);
            TestOutputHelper.WriteLine($"5. Trying to unlist locked package '{LockedPackageId}', version '{LockedPackageVersion}'.");
            processResult = await _commandlineHelper.DeletePackageAsync(LockedPackageId, LockedPackageVersion, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey);
            Assert.True(processResult.ExitCode != 0, "Package delete succeeded, although was expected to fail.");
            Assert.Contains("locked", processResult.StandardError);
        }
    }
}
