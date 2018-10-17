// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NuGetGallery.FunctionalTests.XunitExtensions;

namespace NuGetGallery.FunctionalTests.TyposquattingCheck
{
    public class TyposquattingCheckForUploadPackageTests : GalleryTestBase
    {
        private readonly CommandlineHelper _commandlineHelper;
        private readonly PackageCreationHelper _packageCreationHelper;
        public TyposquattingCheckForUploadPackageTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _commandlineHelper = new CommandlineHelper(TestOutputHelper);
            _packageCreationHelper = new PackageCreationHelper(testOutputHelper);
        }

        [TyposquattingTestFact]
        public async Task UploadTyposquattingPackageAndBlockUser()
        {
            var packageId = "newtonsoft-json";
            string version = "1.0.0";
            string packageFullPath = await _packageCreationHelper.CreatePackageWithMinClientVersion(packageId, version, "2.3");

            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 1, Constants.UploadFailureMessage);
            Assert.Contains("similar", processResult.StandardError);
        }
    }
}