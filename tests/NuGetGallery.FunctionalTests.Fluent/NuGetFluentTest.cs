// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using FluentAutomation;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent
{
    public class NuGetFluentTest : FluentTest
    {
        private static bool _checkForPackageExistence;
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ClientSdkHelper _clientSdkHelper;

        public NuGetFluentTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _clientSdkHelper = new ClientSdkHelper(testOutputHelper);

            SeleniumWebDriver.Bootstrap();
        }

        public async Task UploadPackageIfNecessary(string packageName, string version)
        {
            if (!PackageExists(packageName, version))
            {
                await _clientSdkHelper.UploadNewPackageAndVerify(packageName, version);
            }
        }

        public async Task UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description)
        {
            if (!await _clientSdkHelper.CheckIfPackageVersionExistsInV2AndV3Async(packageName, version))
            {
                await _clientSdkHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description);
            }
        }

        public async Task UploadPackageIfNecessary(string packageName, string version, string minClientVersion, string title, string tags, string description, string licenseUrl)
        {
            if (!await _clientSdkHelper.CheckIfPackageVersionExistsInV2AndV3Async(packageName, version))
            {
                await _clientSdkHelper.UploadNewPackageAndVerify(packageName, version, minClientVersion, title, tags, description, licenseUrl);
            }
        }

        private bool PackageExists(string packageName, string version)
        {
            var found = false;
            for (var i = 0; ((i < 6) && (!found)); i++)
            {
                var requestUrl = UrlHelper.V2FeedRootUrl + @"package/" + packageName + "/" + version + "?t=" + DateTime.Now.Ticks;
                WriteLine("The request URL for checking package existence was: " + requestUrl);
                var packagePageRequest = (HttpWebRequest)WebRequest.Create(requestUrl);

                // Increase timeout to be consistent with the functional tests
                packagePageRequest.Timeout = 2 * 5000;
                try
                {
                    var packagePageResponse = (HttpWebResponse)packagePageRequest.GetResponse();
                    if (packagePageResponse.StatusCode == HttpStatusCode.OK)
                    {
                        found = true;
                    }
                }
                catch (WebException e)
                {
                    WriteLine(e.Message);
                }
            }
            return found;
        }

        public static bool CheckForPackageExistence
        {
            get
            {
                var environmentVariable = Environment.GetEnvironmentVariable("CheckforPackageExistence");
                _checkForPackageExistence = !string.IsNullOrEmpty(environmentVariable) && Convert.ToBoolean(environmentVariable);
                return _checkForPackageExistence;
            }
        }

        protected void WriteLine(string message)
        {
            if (_testOutputHelper == null)
                return;

            _testOutputHelper.WriteLine(message);
        }
    }
}
