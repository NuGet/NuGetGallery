// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class JsonApiControllerFacts
    {
        public class TheAddPackageOwnerMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsFailureWhenPackageNotFound()
            {
                var controller = GetController<JsonApiController>();

                JsonResult result = await controller.AddPackageOwner("foo", "steve");
                dynamic data = result.Data;

                Assert.False(data.success);
                Assert.Equal("Package not found.", data.message);
            }

            [Fact]
            public async Task DoesNotAllowNonPackageOwnerToAddPackageOwner()
            {
                var controller = GetController<JsonApiController>();
                GetMock<IPackageService>()
                    .Setup(svc => svc.FindPackageRegistrationById("foo"))
                    .Returns(new PackageRegistration());

                JsonResult result = await controller.AddPackageOwner("foo", "steve");
                dynamic data = result.Data;

                Assert.False(data.success);
                Assert.Equal("You are not the package owner.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureWhenRequestedNewOwnerDoesNotExist()
            {
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));

                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, "notARealUser");
                dynamic data = result.Data;

                Assert.False(data.success);
                Assert.Equal("Owner not found.", data.message);
            }

            [Fact]
            public async Task CreatesPackageOwnerRequestSendsEmailAndReturnsPendingState()
            {
                var fakes = Get<Fakes>();

                var controller = GetController<JsonApiController>();

                var httpContextMock = GetMock<HttpContextBase>();
                httpContextMock
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner))
                    .Verifiable();

                var packageServiceMock = GetMock<IPackageService>();
                packageServiceMock
                    .Setup(p => p.CreatePackageOwnerRequestAsync(fakes.Package, fakes.Owner, fakes.User))
                    .Returns(Task.FromResult(new PackageOwnerRequest { ConfirmationCode = "confirmation-code" }))
                    .Verifiable();

                var messageServiceMock = GetMock<IMessageService>();
                messageServiceMock
                    .Setup(m => m.SendPackageOwnerRequest(
                        fakes.Owner,
                        fakes.User,
                        fakes.Package,
                        "https://nuget.local/packages/FakePackage/owners/testUser/confirm/confirmation-code"))
                    .Completes()
                    .Verifiable();

                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, fakes.User.Username);
                dynamic data = result.Data;

                Assert.True(data.success);
                Assert.Equal(fakes.User.Username, data.name);
                Assert.True(data.pending);

                httpContextMock.Verify();
                packageServiceMock.Verify();
                messageServiceMock.Verify();
            }
        }
    }
}