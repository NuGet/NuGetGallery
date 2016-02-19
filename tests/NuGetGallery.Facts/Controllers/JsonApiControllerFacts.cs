// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Web;
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

                dynamic result = await controller.AddPackageOwner("foo", "steve");

                Assert.False(result.success);
                Assert.Equal("Package not found.", result.message);
            }

            [Fact]
            public async Task DoesNotAllowNonPackageOwnerToAddPackageOwner()
            {
                var controller = GetController<JsonApiController>();
                GetMock<IPackageService>()
                    .Setup(svc => svc.FindPackageRegistrationById("foo"))
                    .Returns(new PackageRegistration());

                dynamic result = await controller.AddPackageOwner("foo", "steve");

                Assert.False(result.success);
                Assert.Equal("You are not the package owner.", result.message);
            }

            [Fact]
            public async Task ReturnsFailureWhenRequestedNewOwnerDoesNotExist()
            {
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.Owner.ToPrincipal());

                dynamic result = await controller.AddPackageOwner(Fakes.Package.Id, "notARealUser");

                Assert.False(TestUtility.GetAnonymousPropertyValue<bool>(result, "success"));
                Assert.Equal("Owner not found.", TestUtility.GetAnonymousPropertyValue<string>(result, "message"));
            }

            [Fact]
            public async Task CreatesPackageOwnerRequestSendsEmailAndReturnsPendingState()
            {
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.Owner.ToPrincipal());

                GetMock<IPackageService>()
                    .Setup(p => p.CreatePackageOwnerRequestAsync(Fakes.Package, Fakes.Owner, Fakes.User))
                    .Returns(Task.FromResult(new PackageOwnerRequest { ConfirmationCode = "confirmation-code" }));

                dynamic result = await controller.AddPackageOwner(Fakes.Package.Id, Fakes.User.Username);

                Assert.True(result.success);
                Assert.Equal(Fakes.User.Username, result.name);
                Assert.True(result.pending);

                GetMock<IMessageService>()
                    .Verify(m => m.SendPackageOwnerRequest(
                        Fakes.Owner,
                        Fakes.User,
                        Fakes.Package,
                        "https://nuget.local/packages/FakePackage/owners/testUser/confirm/confirmation-code"));
            }
        }
    }
}