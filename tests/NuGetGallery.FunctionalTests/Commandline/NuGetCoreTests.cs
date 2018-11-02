// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Commandline
{
    public class NuGetCoreTests
        : GalleryTestBase
    {
        private readonly ClientSdkHelper _clientSdkHelper;

        public NuGetCoreTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(testOutputHelper);
        }

        [Fact]
        [Description("Downloads a package from the server and validates that the file is present in the local disk")]
        [Priority(0)]
        [Category("P0Tests")]
        public void DownloadPackageWithNuGetCoreTest()
        {
            //try to down load a pre-defined test package - BaseTestPackage.
            _clientSdkHelper.DownloadPackageAndVerify(Constants.TestPackageId);
        }
    }
}
