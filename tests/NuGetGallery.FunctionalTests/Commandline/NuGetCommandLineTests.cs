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
    // All the tests in this class fail due to the following error
    // The package upload via Nuget.exe didn't succeed properly OR package download from V2 feed didn't work. Could not establish trust relationship for the SSL/TLS secure channel
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
            // Temporary work around for the SSL issue, which keeps the upload tests from working on sites with cloudapp.net
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = Constants.TestPackageId; //try to download a pre-defined test package.
                _clientSdkHelper.ClearLocalPackageFolder(packageId, ClientSdkHelper.GetLatestStableVersion(packageId));

                var result = await _commandlineHelper.InstallPackageAsync(packageId, UrlHelper.V2FeedRootUrl, Environment.CurrentDirectory);

                Assert.True(result.ExitCode == 0, Constants.PackageDownloadFailureMessage);
                Assert.True(_clientSdkHelper.CheckIfPackageInstalled(packageId), Constants.PackageInstallFailureMessage);
            }
        }

        [Fact]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadPackageWithNuGetCommandLineTest()
        {
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                await _clientSdkHelper.UploadNewPackageAndVerify(DateTime.Now.Ticks.ToString());
            }
        }

        [Fact]
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task UploadAndDownLoadPackageWithMinClientVersion()
        {
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = DateTime.Now.Ticks + "PackageWithDotCsNames.Cs";
                string version = "1.0.0";
                string packageFullPath = await _packageCreationHelper.CreatePackageWithMinClientVersion(packageId, version, "2.3");

                var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

                Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage);

                var packageVersionExistsInSource = _clientSdkHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl);
                var userMessage = string.Format(Constants.PackageNotFoundAfterUpload, packageId, UrlHelper.V2FeedRootUrl);
                Assert.True(packageVersionExistsInSource, userMessage);

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
}
