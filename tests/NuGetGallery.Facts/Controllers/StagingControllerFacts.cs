// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class StagingControllerFacts : TestContainer
    {
        [Fact]
        public async Task DownloadsAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var content = new MemoryStream();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(currentUser, "PackageA", "1.0.0"))
                .ReturnsAsync(content);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadPackage("PackageA", "1.0.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.PackageContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.nupkg", file.FileDownloadName);
        }

        [Fact]
        public async Task HidesUnauthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(currentUser, "PackageA", "1.0.0"))
                .ReturnsAsync((Stream)null);
            var target = GetController<StagingController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadPackage("PackageA", "1.0.0");

            Assert.IsType<HttpNotFoundResult>(result);
        }
    }
}
