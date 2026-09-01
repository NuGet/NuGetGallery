// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class StagingApiControllerFacts : TestContainer
    {
        [Fact]
        public void ControllerRequiresApiAuthorization()
        {
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiAuthorizeAttribute), inherit: true));
            Assert.NotEmpty(typeof(StagingApiController).GetCustomAttributes(typeof(ApiScopeRequiredAttribute), inherit: true));
        }

        [Fact]
        public void GetsStagedPackages()
        {
            var currentUser = new User("current") { Key = 1 };
            var packages = new[] { new PackageStagingStatus { Id = "PackageA" } };
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetPackages(currentUser, It.IsAny<IEnumerable<Scope>>()))
                .Returns(packages);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = target.GetStagedPackages();

            var json = Assert.IsType<JsonResult>(result);
            Assert.Same(packages, json.Data);
        }

        [Fact]
        public async Task DownloadsAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = new StagedPackage();
            var content = new MemoryStream();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.OpenPackageContentAsync(stagedPackage))
                .ReturnsAsync(content);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(CoreConstants.PackageContentType, file.ContentType);
            Assert.Equal("PackageA.1.0.0.nupkg", file.FileDownloadName);
        }

        [Fact]
        public async Task HidesUnauthorizedPackageDownload()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = new StagedPackage();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(false);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DownloadStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(404, status.StatusCode);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.OpenPackageContentAsync(It.IsAny<StagedPackage>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdatesListedIntentForAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = new StagedPackage();
            var status = new PackageStagingStatus { Listed = true };
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.UpdateListedAsync(stagedPackage, true))
                .Returns(Task.CompletedTask);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.GetStatus(stagedPackage))
                .Returns(status);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateStagedPackageListed(
                "PackageA",
                "1.0.0",
                new UpdateStagedPackageRequest { Listed = true });

            var json = Assert.IsType<JsonResult>(result);
            Assert.Same(status, json.Data);
        }

        [Fact]
        public async Task RejectsMissingUpdateRequest()
        {
            var target = GetController<StagingApiController>();

            var result = await target.UpdateStagedPackageListed("PackageA", "1.0.0", request: null);

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(400, status.StatusCode);
        }

        [Fact]
        public async Task HidesUnauthorizedPackageUpdate()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = new StagedPackage();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(false);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.UpdateStagedPackageListed(
                "PackageA",
                "1.0.0",
                new UpdateStagedPackageRequest { Listed = true });

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(404, status.StatusCode);
            GetMock<IPackageStagingManagementService>().Verify(
                x => x.UpdateListedAsync(It.IsAny<StagedPackage>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletesAuthorizedPackage()
        {
            var currentUser = new User("current") { Key = 1 };
            var stagedPackage = new StagedPackage();
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.FindCurrentStagedPackage("PackageA", "1.0.0"))
                .Returns(stagedPackage);
            GetMock<IPackageStagingAuthorizationService>()
                .Setup(x => x.CanManageWithApiKey(
                    currentUser,
                    It.IsAny<IEnumerable<Scope>>(),
                    stagedPackage))
                .Returns(true);
            GetMock<IPackageStagingManagementService>()
                .Setup(x => x.DeletePackageAsync(stagedPackage))
                .Returns(Task.CompletedTask);
            GetMock<HttpContextBase>()
                .SetupGet(x => x.User)
                .Returns(Fakes.ToPrincipal(currentUser));
            var target = GetController<StagingApiController>();
            target.SetCurrentUser(currentUser);

            var result = await target.DeleteStagedPackage("PackageA", "1.0.0");

            var status = Assert.IsType<HttpStatusCodeResult>(result);
            Assert.Equal(204, status.StatusCode);
        }
    }
}
