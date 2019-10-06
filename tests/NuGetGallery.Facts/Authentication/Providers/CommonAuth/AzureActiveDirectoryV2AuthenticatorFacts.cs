// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using Xunit;

namespace NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2
{
    public class AzureActiveDirectoryV2AuthenticatorFacts
    {
        public class TheGetUIMethod
        {
            [Fact]
            public void GetUIReturnsCorrectValues()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();

                //Act
                var ui = authenticator.GetUI();

                //Assert
                Assert.NotNull(ui);
                Assert.Equal(Strings.MicrosoftAccount_SignInMessage, ui.SignInMessage);
                Assert.Equal(Strings.MicrosoftAccount_AccountNoun, ui.AccountNoun);
                Assert.Equal(Strings.MicrosoftAccount_SignInMessage, ui.RegisterMessage);
            }
        }

        public class TheIsAuthorityForIdentityMethod
        {

            [Fact]
            public void MissingIssuerReturnsFalse()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Identifier
                });

                // Act and assert
                Assert.False(authenticator.IsProviderForIdentity(claimsIdentity));
            }

            [Fact]
            public void MissingTenantReturnsFalse()
            {
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.Identifier,
                });

                // Act and assert
                Assert.False(authenticator.IsProviderForIdentity(claimsIdentity));
            }

            [Fact]
            public void WrongIssuerReturnsFalse()
            {
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Issuer, "wrong issuer", ClaimValueTypes.String, TestData.Authority),
                    TestData.TenantId,
                    TestData.Identifier
                });

                // Act and assert
                Assert.False(authenticator.IsProviderForIdentity(claimsIdentity));
            }

            [Fact]
            public void CorrectClaimsReturnTrue()
            {
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier
                });

                // Act and assert
                Assert.True(authenticator.IsProviderForIdentity(claimsIdentity));
            }
        }

        public class TheGetAuthInformaitonMethod
        {
            [Fact]
            public void ThrowsForMissingIssuerClaim()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                // Issuer/tenant less identity
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Identifier
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetIdentityInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingTenantClaim()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.Identifier
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetIdentityInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingIdentifierClaim()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetIdentityInformation(claimsIdentity));
            }

            [Fact]
            public void MissingNameClaimDoesNotThrow()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier,
                    TestData.Email
                });

                // Act
                var result = authenticator.GetIdentityInformation(claimsIdentity);

                // Assert
                Assert.NotNull(result);
            }

            [Fact]
            public void ThrowsForMissingEmailAndPreferredUsernameClaim()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier,
                    TestData.Name
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetIdentityInformation(claimsIdentity));
            }

            [Fact]
            public void DoesNotThrowForMissingEmailClaimIfPreferredUsernameClaimIsPresent()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier,
                    TestData.Name,
                    TestData.PreferredUsername
                });

                // Act
                var result = authenticator.GetIdentityInformation(claimsIdentity);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(TestData.PreferredUsername.Value, result.Email);
            }

            [Fact]
            public void EmailClaimIsPreferredOverPreferredUsernameClaime()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier,
                    TestData.Name,
                    TestData.Email,
                    TestData.PreferredUsername
                });

                // Act
                var result = authenticator.GetIdentityInformation(claimsIdentity);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(TestData.Email.Value, result.Email);
            }

            [Fact]
            public void ReturnsAuthInformationForCorrectClaims()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var claimsIdentity = new ClaimsIdentity(TestData.GetIdentity());

                // Act
                var result = authenticator.GetIdentityInformation(claimsIdentity);

                //Assert
                Assert.NotNull(result);
                Assert.Equal("blarg", result.Identifier);
                Assert.Equal("bloog", result.Name);
                Assert.Equal(AzureActiveDirectoryV2Authenticator.AuthenticationType.AzureActiveDirectory, result.AuthenticationType);
                Assert.Equal("blarg@bloog.test", result.Email);
                Assert.Equal(TestData.TEST_TENANT_ID, result.TenantId);
            }

            [Fact]
            public void ReturnsFormattedIdentifierForMSA()
            {
                // Arrange
                var authenticator = new AzureActiveDirectoryV2Authenticator();
                var authority = string.Format(AzureActiveDirectoryV2Authenticator.Authority, AzureActiveDirectoryV2Authenticator.PersonalMSATenant);
                var claimsIdentity = new ClaimsIdentity(new[] {
                    new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Issuer, authority, ClaimValueTypes.String, TestData.Authority),
                    new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.TenantId, AzureActiveDirectoryV2Authenticator.PersonalMSATenant, ClaimValueTypes.String, TestData.Authority),
                    new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Identifier, "000000-0000-0000-000A-E45D-63E2-2E4A60", ClaimValueTypes.String, TestData.Authority),
                    TestData.Name,
                    TestData.Email
                });

                // Act
                var result = authenticator.GetIdentityInformation(claimsIdentity);

                //Assert
                Assert.NotNull(result);
                Assert.Equal("0ae45d63e22e4a60", result.Identifier);
                Assert.Equal("bloog", result.Name);
                Assert.Equal(AzureActiveDirectoryV2Authenticator.AuthenticationType.MicrosoftAccount, result.AuthenticationType);
                Assert.Equal("blarg@bloog.test", result.Email);
                Assert.Equal(AzureActiveDirectoryV2Authenticator.PersonalMSATenant, result.TenantId);
            }
        }

        private static class TestData
        {
            public static string TEST_TENANT_ID = "Test-Tenant";
            public static string Authority = string.Format(AzureActiveDirectoryV2Authenticator.Authority, TEST_TENANT_ID);
            public static Claim Issuer = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Issuer, Authority, ClaimValueTypes.String, Authority);
            public static Claim Identifier = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Identifier, "blarg", ClaimValueTypes.String, Authority);
            public static Claim Name = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.Name, "bloog", ClaimValueTypes.String, Authority);
            public static Claim TenantId = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.TenantId, TEST_TENANT_ID, ClaimValueTypes.String, Authority);
            public static Claim Email = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.EmailAddress, "blarg@bloog.test", ClaimValueTypes.String, Authority);
            public static Claim PreferredUsername = new Claim(AzureActiveDirectoryV2Authenticator.V2Claims.PreferredUsername, "preferredUsername@bloog.test", ClaimValueTypes.String, Authority);

            public static ClaimsIdentity GetIdentity()
            {
                return new ClaimsIdentity(new[] {
                    Issuer,
                    TenantId,
                    Identifier,
                    Name,
                    Email,
                    PreferredUsername
                });
            }
        }
    }
}
