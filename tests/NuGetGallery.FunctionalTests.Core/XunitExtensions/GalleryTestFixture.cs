// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests
{
    public class GalleryTestFixture
        : ClearMachineCacheFixture
    {
        public GalleryTestFixture()
        {
            Task.Run(async () =>
            {
                await CheckIfBaseTestPackageExistsAsync();
                await EnsureNuGetExeExistsAsync();
            }).Wait();
        }

        private static async Task EnsureNuGetExeExistsAsync()
        {
            if (!File.Exists("nuget.exe"))
            {
                var nugetExeAddress = "https://nuget.org/nuget.exe";
                using (var webClient = new WebClient())
                {
                    webClient.DownloadFileAsync(new Uri(nugetExeAddress), "nuget.exe");
                }
            }
            else
            {
                var commandlineHelper = new CommandlineHelper(ConsoleTestOutputHelper.New);
                await commandlineHelper.UpdateNugetExeAsync();
            }
        }

        public async Task CheckIfBaseTestPackageExistsAsync()
        {
            // Check if the BaseTestPackage exists in current source and if not upload it.
            // This will be used by the download related tests.
            try
            {
                var clientSdkHelper = new ClientSdkHelper(ConsoleTestOutputHelper.New);
                if (!clientSdkHelper.CheckIfPackageExistsInSource(Constants.TestPackageId, UrlHelper.V2FeedRootUrl))
                {
                    var testOutputHelper = ConsoleTestOutputHelper.New;
                    var commandlineHelper = new CommandlineHelper(testOutputHelper);
                    var packageCreationHelper = new PackageCreationHelper(testOutputHelper);
                    var packageFullPath = await packageCreationHelper.CreatePackage(Constants.TestPackageId, "1.0.0");
                    var processResult = await commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
                    Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage);
                }
            }
            catch (Exception exception)
            {
                var message = string.Format(
                        "The initialization method to pre-upload test package has failed. Hence failing all the tests. Make sure that a package by name {0} exists @ {1} before running tests. Check test run error for details",
                        Constants.TestPackageId, UrlHelper.BaseUrl);
                throw new InvalidOperationException(message, exception);
            }
        }
    }
}