// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class DistIntegrationTests : GalleryTestBase
    {
        private const string Version = "2.8.6";

        public DistIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Ensures the nuget.exe endpoint redirects to an assembly with version " + Version + ".")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task NuGetExeReturnsExpectedVersion()
        {
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var client = new HttpClient())
                await using (var networkStream = await client.GetStreamAsync(UrlHelper.BaseUrl + "nuget.exe"))
                await using (var fileStream = File.Create(tempPath))
                {
                    await networkStream.CopyToAsync(fileStream);
                }

                // FileVersionInfo reads the PE version resource without loading the assembly.
                var versionInfo = FileVersionInfo.GetVersionInfo(tempPath);
                Assert.Equal(Version, versionInfo.ProductVersion);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
