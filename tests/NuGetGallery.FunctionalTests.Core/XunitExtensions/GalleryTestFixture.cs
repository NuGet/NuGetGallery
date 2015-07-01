// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests
{
    public class GalleryTestFixture
        : ClearMachineCacheFixture
    {
        public GalleryTestFixture()
        {
            // Initialize the test collection shared context.
            // Check if functional tests is enabled.
            // If not, do an assert inconclusive.
#if DEBUG
#else
            if (!EnvironmentSettings.RunFunctionalTests.Equals("True", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException("Functional tests are disabled in the current run. Please set environment variable RunFuntionalTests to True to enable them");
            }
#endif

            //supress SSL validation so that we can run tests against staging slot as well.
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
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
                    await clientSdkHelper.UploadNewPackageAndVerify(Constants.TestPackageId);
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