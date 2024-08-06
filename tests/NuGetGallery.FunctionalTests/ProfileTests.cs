// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class ProfileTests : GalleryTestBase
    {
        public ProfileTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Load a profile's avatar")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task GetProfileAvatarAsync()
        {
            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(UrlHelper.GetAvatarUrl("microsoft")))
            using (var memoryStream = new MemoryStream())
            {
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                {
                    await contentStream.CopyToAsync(memoryStream);
                }

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(memoryStream.Length > 0);
            }
        }
    }
}
