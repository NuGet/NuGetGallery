// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.TestBase
{
    /// <summary>
    /// Base class for all the test classes. Has the common functions which individual test classes would use.
    /// </summary>
    [TestClass]
    public class GalleryTestBase
    {
        public TestContext TestContext { get; set; }

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            //Check if functional tests is enabled. If not, do an assert inconclusive.
#if DEBUG
#else
            if (!EnvironmentSettings.RunFunctionalTests.Equals("True", System.StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Functional tests are disabled in the current run. Please set environment variable RunFuntionalTests to True to enable them");
            }
#endif
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; }; //supress SSL validation so that we can run tests against staging slot as well.
            Task.Run(() => CheckIfBaseTestPackageExists()).Wait();
        }

        public static async Task CheckIfBaseTestPackageExists()
        {
            //Check if the BaseTestPackage exists in current source and if not upload it. This will be used by the download related tests.
            try
            {
                if (!ClientSDKHelper.CheckIfPackageExistsInSource(Constants.TestPackageId, UrlHelper.V2FeedRootUrl))
                {
                    await AssertAndValidationHelper.UploadNewPackageAndVerify(Constants.TestPackageId);
                }
            }
            catch (AssertFailedException)
            {
                Assert.Inconclusive("The initialization method to pre-upload test package has failed. Hence failing all the tests. Make sure that a package by name {0} exists @ {1} before running tests. Check test run error for details", Constants.TestPackageId, UrlHelper.BaseUrl);
            }
        }

        [TestInitialize]
        public void TestInit()
        {
            //Clear the machine cache during the start of every test to make sure that we always hit the gallery         .
            ClientSDKHelper.ClearMachineCache();
        }


        [AssemblyCleanup]
        public static void CleanAssembly()
        {

        }
    }
}
