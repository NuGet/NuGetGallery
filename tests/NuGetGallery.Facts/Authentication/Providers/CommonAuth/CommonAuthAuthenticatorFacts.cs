// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using Xunit;

namespace NuGetGallery.Authentication.Providers.CommonAuth
{
    public class CommonAuthAuthenticatorFacts
    {
        public class GetUI
        {
            [Fact]
            public void GetUIReturnsCorrectValues()
            {
                //Arrange
                var authenticator = new CommonAuthAuthenticator();

                //Act
                var ui = authenticator.GetUI();

                //Assert
                Assert.NotNull(ui);
                Assert.Equal(Strings.MicrosoftAccount_SignInMessage, ui.SignInMessage);
                Assert.Equal(Strings.MicrosoftAccount_AccountNoun, ui.AccountNoun);
                Assert.Equal(Strings.MicrosoftAccount_SignInMessage, ui.RegisterMessage);
            }
        }

        public class IsAuthorityForIdentity
        {

            [Fact]
            public void MissingIssuerReturnsFalse()
            {
                //Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Identifier
                });

                // Act and assert
                Assert.False(authenticator.IsAuthorForIdentity(claimsIdentity));
            }

            [Fact]
            public void MissingTenantReturnsFalse()
            {
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.Identifier,
                });

                // Act and assert
                Assert.False(authenticator.IsAuthorForIdentity(claimsIdentity));
            }

            [Fact]
            public void WrongIssuerReturnsFalse()
            {
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    new Claim(CommonAuthAuthenticator.V2Claims.Issuer, "wrong issuer", ClaimValueTypes.String, TestData.Authority),
                    TestData.TenantId,
                    TestData.Identifier
                });

                // Act and assert
                Assert.False(authenticator.IsAuthorForIdentity(claimsIdentity));
            }

            [Fact]
            public void CorrectClaimsReturnTrue()
            {
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier
                });

                // Act and assert
                Assert.True(authenticator.IsAuthorForIdentity(claimsIdentity));
            }
        }

        public class GetAuthInformaiton
        {
            [Fact]
            public void ThrowsForMissingIssuerClaim()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                // Issuer/tenant less identity
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Identifier
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetAuthInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingTenantClaim()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.Identifier
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetAuthInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingIdentifierClaim()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetAuthInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingNameClaim()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetAuthInformation(claimsIdentity));
            }

            [Fact]
            public void ThrowsForMissingEmailClaim()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(new[] {
                    TestData.Issuer,
                    TestData.TenantId,
                    TestData.Identifier,
                    TestData.Name
                });

                // Act and assert
                Assert.Throws<ArgumentException>(() => authenticator.GetAuthInformation(claimsIdentity));
            }

            [Fact]
            public void ReturnsAuthInformationForCorrectClaims()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var claimsIdentity = new ClaimsIdentity(TestData.GetIdentity());

                // Act
                var result = authenticator.GetAuthInformation(claimsIdentity);

                //Assert
                Assert.NotNull(result);
                Assert.Equal("blarg", result.Identifier);
                Assert.Equal("bloog", result.Name);
                Assert.Equal(CommonAuthAuthenticator.AuthenticationType.AzureActiveDirectory, result.AuthenticationType);
                Assert.Equal("blarg@bloog.com", result.Email);
                Assert.Equal(TestData.TEST_TENANT_ID, result.TenantId);
            }

            [Fact]
            public void ReturnsFormattedIdentifierForMSA()
            {
                // Arrange
                var authenticator = new CommonAuthAuthenticator();
                var authority = string.Format(CommonAuthAuthenticator.Authority, CommonAuthAuthenticator.PersonalMSATenant);
                var claimsIdentity = new ClaimsIdentity(new[] {
                    new Claim(CommonAuthAuthenticator.V2Claims.Issuer, authority, ClaimValueTypes.String, TestData.Authority),
                    new Claim(CommonAuthAuthenticator.V2Claims.TenantId, CommonAuthAuthenticator.PersonalMSATenant, ClaimValueTypes.String, TestData.Authority),
                    new Claim(CommonAuthAuthenticator.V2Claims.Identifier, "000000-0000-0000-000A-E45D-63E2-2E4A60", ClaimValueTypes.String, TestData.Authority),
                    TestData.Name,
                    TestData.Email
                });

                // Act
                var result = authenticator.GetAuthInformation(claimsIdentity);

                //Assert
                Assert.NotNull(result);
                Assert.Equal("0ae45d63e22e4a60", result.Identifier);
                Assert.Equal("bloog", result.Name);
                Assert.Equal(CommonAuthAuthenticator.AuthenticationType.MicrosoftAccount, result.AuthenticationType);
                Assert.Equal("blarg@bloog.com", result.Email);
                Assert.Equal(CommonAuthAuthenticator.PersonalMSATenant, result.TenantId);
            }
        }

        private static class TestData
        {
            public static string TEST_TENANT_ID = "Test-Tenant";
            public static string Authority = string.Format(CommonAuthAuthenticator.Authority, TEST_TENANT_ID);
            public static Claim Issuer = new Claim(CommonAuthAuthenticator.V2Claims.Issuer, Authority, ClaimValueTypes.String, Authority);
            public static Claim Identifier = new Claim(CommonAuthAuthenticator.V2Claims.Identifier, "blarg", ClaimValueTypes.String, Authority);
            public static Claim Name = new Claim(CommonAuthAuthenticator.V2Claims.Name, "bloog", ClaimValueTypes.String, Authority);
            public static Claim TenantId = new Claim(CommonAuthAuthenticator.V2Claims.TenantId, TEST_TENANT_ID, ClaimValueTypes.String, Authority);
            public static Claim Email = new Claim(CommonAuthAuthenticator.V2Claims.Email, "blarg@bloog.com", ClaimValueTypes.String, Authority);

            public static ClaimsIdentity GetIdentity()
            {
                return new ClaimsIdentity(new[] {
                    Issuer,
                    TenantId,
                    Identifier,
                    Name,
                    Email
                });
            }
        }
    }
}
