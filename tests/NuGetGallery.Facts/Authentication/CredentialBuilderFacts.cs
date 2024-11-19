// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialBuilderFacts
    {
        public CredentialBuilder Target { get; }

        public class TheCreateShortLivedApiKeyMethod : CredentialBuilderFacts
        {
            [Fact]
            public void CreatesShortLivedApiKey()
            {
                // Act
                var credential = Target.CreateShortLivedApiKey(Expiration, Policy, out var plaintextApiKey);

                // Assert
                Assert.Null(credential.User);
                Assert.Equal(default, credential.UserKey);
                Assert.StartsWith("oy2", plaintextApiKey, StringComparison.Ordinal);
                Assert.Equal(CredentialTypes.ApiKey.V4, credential.Type);
                Assert.Equal("Short-lived API key generated via a federated credential", credential.Description);
                Assert.Equal(Expiration.Ticks, credential.ExpirationTicks);
                Assert.Null(credential.User);

                var scope = Assert.Single(credential.Scopes);
                Assert.Equal(NuGetScopes.All, scope.AllowedAction);
                Assert.Equal(NuGetPackagePattern.AllInclusivePattern, scope.Subject);
                Assert.Same(Policy.PackageOwner, scope.Owner);
            }

            [Fact]
            public void RejectsMissingPackageOwner()
            {
                // Arrange
                Policy.PackageOwner = null;

                // Act
                Assert.Throws<ArgumentException>(() => Target.CreateShortLivedApiKey(Expiration, Policy, out var plaintextApiKey));
            }

            [Theory]
            [InlineData(-1)]
            [InlineData(0)]
            [InlineData(61)]
            public void RejectsOutOfRangeExpiration(int expirationMinutes)
            {
                // Arrange
                Expiration = TimeSpan.FromMinutes(expirationMinutes);

                // Act
                Assert.Throws<ArgumentOutOfRangeException>(() => Target.CreateShortLivedApiKey(Expiration, Policy, out var plaintextApiKey));
            }

            public FederatedCredentialPolicy Policy { get; }

            public TheCreateShortLivedApiKeyMethod()
            {
                Policy = new FederatedCredentialPolicy
                {
                    Key = 23,
                    PackageOwner = new User { Key = 42 },
                    CreatedBy = new User { Key = 43 },
                };
            }
        }

        public TimeSpan Expiration { get; set; }

        public CredentialBuilderFacts()
        {
            Expiration = TimeSpan.FromMinutes(13);

            Target = new CredentialBuilder();
        }
    }
}
