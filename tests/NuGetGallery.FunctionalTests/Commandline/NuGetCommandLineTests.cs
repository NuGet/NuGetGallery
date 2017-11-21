// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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
        public async Task DownloadPackageWithNuGetCommandLineTest()
        {
            string packageId = Constants.TestPackageId; //try to download a pre-defined test package.
            _clientSdkHelper.ClearLocalPackageFolder(packageId, ClientSdkHelper.GetLatestStableVersion(packageId));

            var result = await _commandlineHelper.InstallPackageAsync(packageId, UrlHelper.V2FeedRootUrl, Environment.CurrentDirectory);

            Assert.True(result.ExitCode == 0, Constants.PackageDownloadFailureMessage);
            Assert.True(_clientSdkHelper.CheckIfPackageInstalled(packageId), Constants.PackageInstallFailureMessage);
        }

        [Fact]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadPackageWithNuGetCommandLineTest()
        {
            await _clientSdkHelper.UploadNewPackageAndVerify(DateTime.Now.Ticks.ToString());
        }

        [Fact]
        [Description("Uses scoped API keys to push and unlist packages using Nuget.exe")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task VerifyScopedApiKeys()
        {
            // Arrange
            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var commandlineHelper = new CommandlineHelper(TestOutputHelper);

            var packageId = "ScopedApiKeysTest_" + DateTime.Now.Ticks;
            var version1 = "1.0.0";
            var version2= "2.0.0";

            string package1FullPath = null;
            string package2FullPath = null;

            try
            {
                package1FullPath = await packageCreationHelper.CreatePackage(packageId, version1);
                package2FullPath = await packageCreationHelper.CreatePackage(packageId, version2);

                // 1. Try to upload package using 'unlist' api key => expect failure
                TestOutputHelper.WriteLine($"1. Trying to upload package '{packageId}', version '{version1}' using 'unlist' API key. Expected result: failure.");
                var processResult = await commandlineHelper.UploadPackageAsync(package1FullPath, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey_Unlist);
                Assert.True(processResult.ExitCode != 0, "Package push succeeded, although was expected to fail.");

                // 2. Try to upload package using 'push version' api key => expect failure
                TestOutputHelper.WriteLine($"2. Trying to upload package '{packageId}', version '{version1}' using 'push version' API key. Expected result: failure.");
                processResult = await commandlineHelper.UploadPackageAsync(package1FullPath, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey_PushVersion);
                Assert.True(processResult.ExitCode != 0, "Package push succeeded, although was expected to fail.");

                // 3. Upload package using 'push' api key => expect success
                TestOutputHelper.WriteLine($"3. Trying to upload package '{packageId}', version '{version1}' using 'push' API key. Expected result: success.");
                await _clientSdkHelper.UploadExistingPackage(package1FullPath, EnvironmentSettings.TestAccountApiKey_Push);

                // 4. Upload new version of package using 'push version' api key => expect success
                TestOutputHelper.WriteLine($"4. Trying to upload package '{packageId}', version '{version2}' using 'push version' API key. Expected result: success.");
                await _clientSdkHelper.UploadExistingPackage(package2FullPath, EnvironmentSettings.TestAccountApiKey_PushVersion);

                // Verify the existence of the two pushed packages.
                await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version1);
                await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version2);

                // 5. Try unlisting package version1 using 'push' api key => expect failture
                TestOutputHelper.WriteLine($"5. Trying to unlist package '{packageId}', version '{version1}' using 'push' API key. Expected result: failure.");
                processResult = await commandlineHelper.DeletePackageAsync(packageId, version1, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey_Push);
                Assert.True(processResult.ExitCode != 0, "Package delete succeeded, although was expected to fail.");

                // 6. Try unlisting package version2 using 'push version' api key => expect failture
                TestOutputHelper.WriteLine($"6. Trying to unlist package '{packageId}', version '{version2}' using 'push' API key. Expected result: failure.");
                processResult = await commandlineHelper.DeletePackageAsync(packageId, version2, UrlHelper.V2FeedPushSourceUrl, EnvironmentSettings.TestAccountApiKey_PushVersion);
                Assert.True(processResult.ExitCode != 0, "Package delete succeeded, although was expected to fail.");

                // 7. Unlist both packages using 'unlist' api key => expect succees
                TestOutputHelper.WriteLine($"7. Trying to unlist package '{packageId}', version '{version1}' using 'unlist' API key. Expected result: success.");
                await _clientSdkHelper.UnlistPackage(packageId, version1, EnvironmentSettings.TestAccountApiKey_Unlist);

                TestOutputHelper.WriteLine($"8. Trying to unlist package '{packageId}', version '{version2}' using 'unlist' API key. Expected result: success.");
                await _clientSdkHelper.UnlistPackage(packageId, version2, EnvironmentSettings.TestAccountApiKey_Unlist);

            }
            finally
            {
                _clientSdkHelper.CleanCreatedPackage(package1FullPath);
                _clientSdkHelper.CleanCreatedPackage(package2FullPath);
            }
        }

        [Fact]
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndDownLoadPackageWithMinClientVersion()
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
    }
}
