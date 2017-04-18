// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Auditing;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;
using System.Web;
using System.Collections.Specialized;
using System.Security.Principal;
using System.Security.Claims;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class SecurityPolicyServiceFacts
    {
        public class TheAddSecurityPolicyMethod : TestContainer
        {
            [Fact]
            public async Task ThrowsForUnsupportedSecurityPolicy()
            {
                // Arrange
                var policy = new Mock<SecurityPolicy>().Object;
                var user = new User("a");

                // Act
                var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
                    await CreateService().AddSecurityPolicyAsync(user, policy));

                // Assert
                Assert.NotNull(ex);
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var service = CreateService();
                var policy = new PackageVerificationKeysPolicy();
                var user = new User("a");

                // Act
                await service.AddSecurityPolicyAsync(user, policy);

                // Assert
                Assert.True(service.Auditing.WroteRecord<PackageVerificationKeysPolicyAuditRecord>(ar =>
                    ar.Action == UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_AddPolicy &&
                    ar.Username == "a"));

                Assert.Equal(1, user.SecurityPolicies.Count);
            }

            [Theory]
            [InlineData("{\"a\":\"all\", \"s\":\"*\"}")]
            [InlineData("{\"a\":\"package:push\", \"s\":\"*\"}")]
            [InlineData("{\"a\":\"package:pushversion\", \"s\":\"*\"}")]
            public Task ExpiresApiKeysWithPushScopes(string scope)
            {
                return TestExpiresApiKey(scope, true);
            }

            [Theory]
            [InlineData("{\"a\":\"package:verify\", \"s\":\"*\"}")]
            [InlineData("{\"a\":\"package:unlist\", \"s\":\"*\"}")]
            public Task DoesNotExpireKeysWithoutPushScope(string scope)
            {
                return TestExpiresApiKey(scope, false);
            }

            [Fact]
            public Task ExpiresLegacyApiKey()
            {
                // Arrange
                var credential = new Credential(CredentialTypes.ApiKey.V2, string.Empty, TimeSpan.FromDays(7));

                // Act & Assert
                return TestExpiresApiKey(credential, true);
            }

            private Task TestExpiresApiKey(string scope, bool shouldExpire)
            {
                // Arrange
                var credential = new Credential(CredentialTypes.ApiKey.V2, string.Empty, TimeSpan.FromDays(7));
                credential.Scopes.Add(JsonConvert.DeserializeObject<Scope>(scope));

                return TestExpiresApiKey(credential, shouldExpire);
            }

            private async Task TestExpiresApiKey(Credential credential, bool shouldExpire)
            {
                // Arrange
                var policy = new PackageVerificationKeysPolicy();
                var user = new User("a");
                user.Credentials.Add(credential);

                // Act
                await CreateService().AddSecurityPolicyAsync(user, policy);

                // Assert
                Assert.Equal(1, user.SecurityPolicies.Count);
                Assert.Equal(1, user.Credentials.Count);

                var expires = user.Credentials.First().Expires.Value;
                Assert.Equal(shouldExpire, expires <= DateTime.UtcNow.AddDays(1));
            }

            private SecurityPolicyService CreateService()
            {
                return new SecurityPolicyService(Get<IEntitiesContext>(), Get<IAuditingService>(), new DiagnosticsService());
            }
        }

        public class TheCanCreatePackageMethod : TestContainer
        {
            [Fact]
            public async Task WritesAuditRecordIfUserSubscribed()
            {
                var service = CreateService();
                var httpContext = CreateHttpContext("1.0.0");
                var user = new User("a");
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());

                // Act
                var result = await service.CanCreatePackageAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.True(service.Auditing.WroteRecord<PackageVerificationKeysPolicyAuditRecord>(ar =>
                    ar.Action == UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_CreatePackage &&
                    ar.Username == "a" &&
                    ar.PushedPackage.Id == "b" && ar.PushedPackage.Version == "1.2.3" &&
                    // Writes error due to client version 1.0.0.
                    !string.IsNullOrEmpty(ar.ErrorMessage)));
            }

            [Fact]
            public async Task DoesNotWriteAuditRecordIfUserNotSubscribed()
            {
                var service = CreateService();
                var httpContext = CreateHttpContext("5.0.0");
                var user = new User("a");

                // Act
                var result = await service.CanCreatePackageAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(service.Auditing.WroteRecord<PackageVerificationKeysPolicyAuditRecord>(ar =>
                    ar.Action == UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_CreatePackage));
            }

            [Fact]
            public async Task ReturnsSuccessIfUserNotSubscribed()
            {
                var httpContext = CreateHttpContext(clientVersion: "1.0.0");
                var user = new User("a");

                // Act
                var result = await CreateService().CanCreatePackageAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(result.HasError);
                Assert.Null(result.ErrorMessage);
            }

            [Theory]
            [InlineData("4.1.0")]
            [InlineData("10.0.0")]
            public async Task ReturnsSuccessIfClientVersionMet(string clientVersion)
            {
                var httpContext = CreateHttpContext(clientVersion);
                var user = new User("a");
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());

                // Act
                var result = await CreateService().CanCreatePackageAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(result.HasError);
                Assert.Null(result.ErrorMessage);
            }

            [Theory]
            [InlineData("")]
            [InlineData("4.0.0")]
            public async Task ReturnsErrorIfClientVersionNotMet(string clientVersion)
            {
                var httpContext = CreateHttpContext(clientVersion);
                var user = new User("a");
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());

                // Act
                var result = await CreateService().CanCreatePackageAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.True(result.HasError);
                Assert.NotEmpty(result.ErrorMessage);
            }

            private SecurityPolicyService CreateService()
            {
                return new SecurityPolicyService(Get<IEntitiesContext>(), Get<IAuditingService>(), new DiagnosticsService());
            }

            private HttpContextBase CreateHttpContext(string clientVersion)
            {
                var headers = new NameValueCollection();
                if (!string.IsNullOrEmpty(clientVersion))
                {
                    headers[Constants.ClientVersionHeaderName] = clientVersion;
                }

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.Headers).Returns(headers);

                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                return httpContext.Object;
            }
        }

        public class TheCanVerifyPackageKeyMethod : TestContainer
        {
            [Fact]
            public async Task WritesAuditRecordIfUserSubscribed()
            {
                // Arrange
                var service = CreateService();
                var user = new User("a");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, Guid.Empty.ToString()));
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());
                var httpContext = CreateHttpContext(user);

                // Act
                await service.CanVerifyPackageKeyAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.True(service.Auditing.WroteRecord<PackageVerificationKeysPolicyAuditRecord>(ar =>
                    ar.Action == UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_VerifyPackageKey &&
                    ar.Username == "a" &&
                    ar.PushedPackage.Id == "b" && ar.PushedPackage.Version == "1.2.3" &&
                    // Policy error due to ApiKey.V2.
                    !string.IsNullOrEmpty(ar.ErrorMessage)));
            }

            [Fact]
            public async Task DoesNotWriteAuditRecordIfUserNotSubscribed()
            {
                // Arrange
                var service = CreateService();
                var user = new User("a");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, Guid.Empty.ToString()));
                var httpContext = CreateHttpContext(user);

                // Act
                await service.CanVerifyPackageKeyAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(service.Auditing.WroteRecord<PackageVerificationKeysPolicyAuditRecord>(ar =>
                    ar.Action == UserSecurityPolicyAuditAction.PackageVerificationKeysPolicy_VerifyPackageKey));
            }

            [Fact]
            public async Task ReturnsSuccessIfUserNotSubscribed()
            {
                // Arrange
                var service = CreateService();
                var user = new User("a");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, Guid.Empty.ToString()));
                var httpContext = CreateHttpContext(user);

                // Act
                var result = await service.CanVerifyPackageKeyAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(result.HasError);
                Assert.Null(result.ErrorMessage);
            }

            [Fact]
            public async Task ReturnsSuccessIfVerificationKeyUsed()
            {
                // Arrange
                var service = CreateService();
                var user = new User("a");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.VerifyV1, Guid.Empty.ToString()));
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());
                var httpContext = CreateHttpContext(user);

                // Act
                var result = await service.CanVerifyPackageKeyAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.False(result.HasError);
                Assert.Null(result.ErrorMessage);
            }

            [Fact]
            public async Task ReturnsErrorIfVerificationKeyNotUsed()
            {
                // Arrange
                var service = CreateService();
                var user = new User("a");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, Guid.Empty.ToString()));
                user.SecurityPolicies.Add(new PackageVerificationKeysPolicy());
                var httpContext = CreateHttpContext(user);

                // Act
                var result = await service.CanVerifyPackageKeyAsync(user, httpContext, "b", "1.2.3");

                // Assert
                Assert.True(result.HasError);
                Assert.NotEmpty(result.ErrorMessage);
            }

            private SecurityPolicyService CreateService()
            {
                return new SecurityPolicyService(Get<IEntitiesContext>(), Get<IAuditingService>(), new DiagnosticsService());
            }

            private HttpContextBase CreateHttpContext(User user)
            {
                var identity = AuthenticationService.CreateIdentity(user,
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, Guid.Empty.ToString()));

                var principal = new Mock<IPrincipal>(MockBehavior.Strict);
                principal.Setup(p => p.Identity).Returns(identity);

                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.User).Returns(principal.Object);

                return httpContext.Object;
            }
        }
    }
}