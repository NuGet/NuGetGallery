// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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
            // Arrange
            using (var client = new HttpClient())
            using (var networkStream = await client.GetStreamAsync(UrlHelper.BaseUrl + "nuget.exe"))
            using (var memoryStream = new MemoryStream())
            {
                await networkStream.CopyToAsync(memoryStream);
                var assembly = Assembly.Load(memoryStream.ToArray());

                // Act
                var attributes = assembly
                    .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                    .ToList();

                // Assert
                Assert.Single(attributes);
                Assert.Equal(Version, attributes[0].InformationalVersion);
            }
        }
    }
}
