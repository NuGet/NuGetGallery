// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.BasicPages
{
    public class MinClientVersionFromUITest : NuGetFluentTest
    {
        public MinClientVersionFromUITest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Upload a package with a MinClientVersion and validate the min client version number in the package page.")]
        [Priority(2)]
        public async Task MinClientVersionFromUI()
        {
            // Use the same package name, but force the version to be unique.
            var packageName = "NuGetGallery.FunctionalTests.Fluent.MinClientVersionFromUITest";
            var version = "1.0.0";
            await UploadPackageIfNecessary(packageName, version, "2.7", packageName, "minclientversion", "A package with a MinClientVersion set for testing purpose only");

            // Validate that the minclientversion is shown to the user on the package page.
            I.Open(UrlHelper.BaseUrl + @"packages/" + packageName + "/" + version);
            var expectedMinClientVersion = @"p:contains('Requires NuGet 2.7 or higher')";

            I.Expect.Count(1).Of(expectedMinClientVersion);
        }
    }
}
