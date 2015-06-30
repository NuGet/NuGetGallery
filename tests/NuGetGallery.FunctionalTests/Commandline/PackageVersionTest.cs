// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Commandline
{
    public class PackageVersionTest
        : GalleryTestBase
    {
        private readonly ClientSdkHelper _clientSdkHelper;

        public PackageVersionTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(testOutputHelper);
        }

        [Fact]
        [Description("Upload multiple versions of a package and see if it gets uploaded properly")]
        [Priority(0)]
        [Category("P1Tests")]
        public async Task UploadMultipleVersionOfPackage()
        {
            var packageId = string.Format("TestMultipleVersion.{0}", DateTime.Now.Ticks);

            await _clientSdkHelper.UploadNewPackageAndVerify(packageId);
            await _clientSdkHelper.UploadNewPackageAndVerify(packageId, "2.0.0");

            int actualCount = _clientSdkHelper.GetVersionCount(packageId);

            var userMessage = string.Format(" 2 versions of package {0} not found after uploading. Actual versions found {1}", packageId, actualCount);
            Assert.True(actualCount.Equals(2), userMessage);
        }
    }
}
