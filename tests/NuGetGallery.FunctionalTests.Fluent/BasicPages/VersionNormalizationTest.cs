// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.BasicPages
{

    public class VersionNormalizationTest : NuGetFluentTest
    {
        public VersionNormalizationTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Verify normalization of package version numbers.")]
        [Priority(2)]
        public async Task VersionNormalization()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.VersionNormalizationTest";

            if (CheckForPackageExistence)
            {
                await UploadPackageIfNecessary(packageName, "0.0.0.0");
                await UploadPackageIfNecessary(packageName, "1.0.0.0");
                await UploadPackageIfNecessary(packageName, "1.0.0.1");
                await UploadPackageIfNecessary(packageName, "1.0.1.0");
                await UploadPackageIfNecessary(packageName, "1.0.1.1");
                await UploadPackageIfNecessary(packageName, "1.1.0.0");
                await UploadPackageIfNecessary(packageName, "1.1.0");
                await UploadPackageIfNecessary(packageName, "10.10.10.10");
                await UploadPackageIfNecessary(packageName, "20.0.20.0");
                await UploadPackageIfNecessary(packageName, "00300.00.0.00300");
            }

            I.Open(UrlHelper.BaseUrl + "/packages/" + packageName);
            I.Expect.Exists("a:contains(' 0.0.0')");
            I.Expect.Exists("a:contains(' 1.0.0')");
            I.Expect.Exists("a:contains(' 1.0.0.1')");
            I.Expect.Exists("a:contains(' 1.0.1')");
            I.Expect.Exists("a:contains(' 1.0.1.1')");
            I.Expect.Exists("a:contains(' 1.1.0')");
            I.Expect.Exists("a:contains(' 10.10.10.10')");
            I.Expect.Exists("a:contains(' 20.0.20')");
            I.Expect.Exists("span:contains(' 300.0.0.300')");
        }
    }
}
