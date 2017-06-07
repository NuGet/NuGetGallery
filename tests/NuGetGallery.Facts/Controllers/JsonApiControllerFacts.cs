// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;
using NuGetGallery.Security;
using NuGetGallery.Configuration;
using System.Net.Mail;

namespace NuGetGallery.Controllers
{
    public class JsonApiControllerFacts
    {
        public class TheGetAddPackageOwnerConfirmationMethod : TestContainer
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfPackageIdMissing(string id)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.GetAddPackageOwnerConfirmationAsync(id, "user"));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfUsernameMissing(string username)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.GetAddPackageOwnerConfirmationAsync("package", username));
            }

            [Fact]
            public async Task ReturnsFailureIfPackageNotFound()
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync("package", "user");
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Package not found.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfUserIsNotPackageOwner()
            {
                // Arrange
                var controller = GetController<JsonApiController>();
                GetMock<IPackageService>()
                    .Setup(svc => svc.FindPackageRegistrationById("package"))
                    .Returns(new PackageRegistration());

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync("package", "nonOwner");
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("You are not the package owner.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfOwnerIsNotRealUser()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, "nonUser");
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Owner not found.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfNewOwnerIsNotConfirmed()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                fakes.User.UnconfirmedEmailAddress = fakes.Owner.EmailAddress;
                fakes.User.EmailAddress = null;

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Sorry, 'testUser' hasn't verified their email account yet and we cannot proceed with the request.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfCurrentUserNotFound()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                GetMock<IUserService>()
                    .Setup(s => s.FindByUsername(fakes.Owner.Username))
                    .ReturnsNull();

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Current user not found.", data.message);
            }

            [Fact]
            public async Task ReturnsDefaultConfirmationIfNoPolicyPropagation()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.True(data.success);
                Assert.Equal("Please confirm if you want to proceed adding 'testUser' as a co-owner of this package.", data.confirmation);
            }

            [Fact]
            public async Task ReturnsDetailedConfirmationIfNewOwnerPropagatesPolicy()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<IAppConfiguration>().Setup(c => c.GalleryOwner).Returns(new MailAddress("support@example.com"));
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                fakes.User.SecurityPolicies = (new RequireSecurePushForCoOwnersPolicy().Policies).ToList();

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.True(data.success);
                Assert.StartsWith(
                    "User 'testUser' has the following requirements that will be enforced for all co-owners once the user accepts ownership of this package:",
                    data.confirmation);
            }

            [Fact]
            public async Task ReturnsDetailedConfirmationIfCurrentOwnerPropagatesPolicy()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<IAppConfiguration>().Setup(c => c.GalleryOwner).Returns(new MailAddress("support@example.com"));
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                fakes.Owner.SecurityPolicies = (new RequireSecurePushForCoOwnersPolicy().Policies).ToList();

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.True(data.success);
                Assert.StartsWith(
                    "Owner(s) 'testPackageOwner' has (have) the following requirements that will be enforced for user 'testUser' once the user accepts ownership of this package:",
                    data.confirmation);
            }

            [Fact]
            public async Task ReturnsDetailedConfirmationIfPendingOwnerPropagatesPolicy()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<IAppConfiguration>().Setup(c => c.GalleryOwner).Returns(new MailAddress("support@example.com"));
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));

                fakes.ShaUser.SecurityPolicies = (new RequireSecurePushForCoOwnersPolicy().Policies).ToList();
                var pendingOwner = new PackageOwnerRequest()
                {
                    PackageRegistrationKey = fakes.Package.Key,
                    NewOwner = fakes.ShaUser
                };
                GetMock<IEntityRepository<PackageOwnerRequest>>()
                    .Setup(r => r.GetAll())
                    .Returns((new [] { pendingOwner }).AsQueryable());

                // Act
                var result = await controller.GetAddPackageOwnerConfirmationAsync(fakes.Package.Id, fakes.User.Username);
                dynamic data = ((JsonResult)result).Data;

                // Assert
                Assert.True(data.success);
                Assert.StartsWith(
                    "Pending owner(s) 'testShaUser' has (have) the following requirements that will be enforced for all co-owners, including 'testUser', once ownership requests are accepted:",
                    data.confirmation);
            }
        }

        public class TheAddPackageOwnerMethod : TestContainer
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfPackageIdMissing(string id)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.AddPackageOwner(id, "user", string.Empty));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfUsernameMissing(string username)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.AddPackageOwner("package", username, string.Empty));
            }

            [Fact]
            public async Task ReturnsFailureWhenPackageNotFound()
            {
                var controller = GetController<JsonApiController>();

                JsonResult result = await controller.AddPackageOwner("foo", "steve", "message");
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

                JsonResult result = await controller.AddPackageOwner("foo", "steve", "message");
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

                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, "notARealUser", "message");
                dynamic data = result.Data;

                Assert.False(data.success);
                Assert.Equal("Owner not found.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfNewOwnerIsNotConfirmed()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                fakes.User.UnconfirmedEmailAddress = fakes.Owner.EmailAddress;
                fakes.User.EmailAddress = null;

                // Act
                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, fakes.User.Username, "message");
                dynamic data = result.Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Sorry, 'testUser' hasn't verified their email account yet and we cannot proceed with the request.", data.message);
            }

            [Fact]
            public async Task ReturnsFailureIfCurrentUserNotFound()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var controller = GetController<JsonApiController>();
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                GetMock<IUserService>()
                    .Setup(s => s.FindByUsername(fakes.Owner.Username))
                    .ReturnsNull();

                // Act
                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, fakes.User.Username, "message");
                dynamic data = result.Data;

                // Assert
                Assert.False(data.success);
                Assert.Equal("Current user not found.", data.message);
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
                        "/packages/FakePackage/",
                        "https://nuget.local/packages/FakePackage/owners/testUser/confirm/confirmation-code",
                        "Hello World! Html Encoded &lt;3",
                        ""))
                    .Verifiable();

                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, fakes.User.Username, "Hello World! Html Encoded <3");
                dynamic data = result.Data;

                Assert.True(data.success);
                Assert.Equal(fakes.User.Username, data.name);
                Assert.True(data.pending);

                httpContextMock.Verify();
                packageServiceMock.Verify();
                messageServiceMock.Verify();
            }

            [Fact]
            public async Task SendsPackageOwnerRequestEmailWhereNewOwnerPropagatesPolicy()
            {
                // Arrange & Act
                var fakes = Get<Fakes>();
                var policyMessage = await GetSendPackageOwnerRequestPolicyMessage(fakes, fakes.User);

                // Assert
                Assert.StartsWith(
                    "Note: The following policies will be enforced on package co-owners once you accept this request.",
                    policyMessage);
            }

            [Fact]
            public async Task SendsPackageOwnerRequestEmailWhereCurrentOwnerPropagatesPolicy()
            {
                // Arrange & Act
                var fakes = Get<Fakes>();
                var policyMessage = await GetSendPackageOwnerRequestPolicyMessage(fakes, fakes.Owner);

                // Assert
                Assert.StartsWith(
                    "Note: Owner(s) 'testPackageOwner' has (have) the following policies that will be enforced on your account once you accept this request.",
                    policyMessage);
            }

            [Fact]
            public async Task SendsPackageOwnerRequestEmailWherePendingOwnerPropagatesPolicy()
            {
                // Arrange & Act
                var fakes = Get<Fakes>();

                var pendingOwner = new PackageOwnerRequest()
                {
                    PackageRegistrationKey = fakes.Package.Key,
                    NewOwner = fakes.ShaUser
                };
                GetMock<IEntityRepository<PackageOwnerRequest>>()
                    .Setup(r => r.GetAll())
                    .Returns((new[] { pendingOwner }).AsQueryable());

                var policyMessage = await GetSendPackageOwnerRequestPolicyMessage(fakes, fakes.ShaUser);

                // Assert
                Assert.StartsWith(
                    "Note: Pending owner(s) 'testShaUser' has (have) the following policies that will be enforced on your account once ownership requests are accepted.",
                    policyMessage);
            }

            private async Task<string> GetSendPackageOwnerRequestPolicyMessage(Fakes fakes, User userToSubscribe)
            {
                // Arrange
                var controller = GetController<JsonApiController>();
                GetMock<IAppConfiguration>().Setup(c => c.GalleryOwner).Returns(new MailAddress("support@example.com"));
                GetMock<HttpContextBase>()
                    .Setup(c => c.User)
                    .Returns(Fakes.ToPrincipal(fakes.Owner));
                userToSubscribe.SecurityPolicies = (new RequireSecurePushForCoOwnersPolicy().Policies).ToList();

                var packageServiceMock = GetMock<IPackageService>();
                packageServiceMock
                    .Setup(p => p.CreatePackageOwnerRequestAsync(fakes.Package, fakes.Owner, fakes.User))
                    .Returns(Task.FromResult(new PackageOwnerRequest { ConfirmationCode = "confirmation-code" }))
                    .Verifiable();

                string actualMessage = string.Empty;
                GetMock<IMessageService>()
                    .Setup(m => m.SendPackageOwnerRequest(
                        fakes.Owner,
                        fakes.User,
                        fakes.Package,
                        "/packages/FakePackage/",
                        "https://nuget.local/packages/FakePackage/owners/testUser/confirm/confirmation-code",
                        string.Empty,
                        It.IsAny<string>()))
                    .Callback<User, User, PackageRegistration, string, string, string, string>(
                        (from, to, pkg, pkgUrl, cnfUrl, msg, policyMsg) => actualMessage = policyMsg);

                // Act
                JsonResult result = await controller.AddPackageOwner(fakes.Package.Id, fakes.User.Username, string.Empty);
                dynamic data = result.Data;

                // Assert
                Assert.True(data.success);
                Assert.False(String.IsNullOrEmpty(actualMessage));
                return actualMessage;
            }
        }

        public class TheRemovePackageOwnerMethod : TestContainer
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfPackageIdMissing(string id)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.RemovePackageOwner(id, "user"));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public async Task ThrowsArgumentNullIfUsernameMissing(string username)
            {
                // Arrange
                var controller = GetController<JsonApiController>();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => controller.RemovePackageOwner("package", username));
            }
        }
    }
}