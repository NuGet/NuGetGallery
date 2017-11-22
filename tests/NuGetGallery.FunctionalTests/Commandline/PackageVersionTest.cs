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
        [Priority(1)]
        [Category("P1Tests")]
        public async Task UploadMultipleVersionOfPackage()
        {
            var packageId = string.Format("TestMultipleVersion.{0}", DateTime.Now.Ticks);

            await _clientSdkHelper.UploadNewPackage(packageId, "1.0.0");
            await _clientSdkHelper.UploadNewPackage(packageId, "2.0.0");

            await _clientSdkHelper.VerifyPackageExistsInV2AndV3Async(packageId, "1.0.0");
            await _clientSdkHelper.VerifyPackageExistsInV2AndV3Async(packageId, "2.0.0");

            await _clientSdkHelper.VerifyVersionCountAsync(packageId, 2);
        }
    }
}
