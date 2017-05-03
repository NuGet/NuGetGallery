// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Security.Claims;
using NuGet.Frameworks;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class ExtensionMethodsFacts
    {
        public class TheGetCurrentApiKeyCredentialMethod
        {
            [Theory]
            [InlineData("apikey.v2")]
            [InlineData("apikey.verify.v1")]
            public void ReturnsApiKeyMatchingClaim(string credentialType)
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(credentialType, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, "B"));

                // Act
                var credential = user.GetCurrentApiKeyCredential(identity);

                // Assert
                Assert.Equal("B", credential.Value);
            }

            [Fact]
            public void ReturnsNullIfNoApiKeyClaim()
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.LocalUser);
                
                // Act & Assert
                Assert.Null(user.GetCurrentApiKeyCredential(identity));
            }
        }

        public class TheHasPackageVerifyScopeClaimMethod
        {
            [Fact]
            public void ReturnsTrueIfPackageVerifyScopeClaimExists()
            {
                // Arrange
                var scope = "[{\"a\":\"package:verify\", \"s\":\"foo\"}]";
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scope));

                // Act & Assert
                Assert.True(identity.HasPackageVerifyScopeClaim());
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:unlist\", \"s\":\"foo\"}]")]
            public void ReturnsFalseIfPackageVerifyScopeClaimDoesNotExist(string scope)
            {
                // Arrange
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scope));

                // Act & Assert
                Assert.False(identity.HasPackageVerifyScopeClaim());
            }

            [Fact]
            public void ReturnsFalseIfScopeClaimDoesNotExist()
            {
                // Arrange
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                // Act & Assert
                Assert.False(identity.HasPackageVerifyScopeClaim());
            }
        }

        public class TheToFriendlyNameMethod
        {
            [Theory]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Silverlight 4.0, Windows 8.0, WindowsPhone 7.1)", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable-net45+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Windows 8.0)", "portable40-net40+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable45-net45+win8")]
            public void CorrectlyConvertsShortNameToFriendlyName(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName();
                Assert.Equal(expected, actual);
            }

            [Theory]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library", "portable-net45+win8")]
            [InlineData("Portable Class Library", "portable40-net45+win8")]
            [InlineData("Portable Class Library", "portable45-net45+win8")]
            public void DoesNotRecurseWhenAllowRecurseProfileFalse(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName(allowRecurseProfile: false);
                Assert.Equal(expected, actual);
            }
        }
    }
}
