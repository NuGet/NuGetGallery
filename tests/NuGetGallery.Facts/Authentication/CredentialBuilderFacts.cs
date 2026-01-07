// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialBuilderFacts
    {
        public class TheCreateShortLivedApiKeyMethod : CredentialBuilderFacts
        {
            [Fact]
            public void CreatesShortLivedApiKeyWithV5()
            {
                // Act
                var credential = Target.CreateShortLivedApiKey(Expiration, Policy, galleryEnvironment: ServicesConstants.DevelopmentEnvironment, out var plaintextApiKey);

                // Assert
                Assert.Null(credential.User);
                Assert.Equal(default, credential.UserKey);
                Assert.Equal(CredentialTypes.ApiKey.V5, credential.Type);
                Assert.Equal("Short-lived API key generated via a federated credential", credential.Description);
                Assert.Equal(Expiration.Ticks, credential.ExpirationTicks);

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
                Assert.Throws<ArgumentException>(() => Target.CreateShortLivedApiKey(Expiration, Policy, It.IsAny<string>(), out var plaintextApiKey));
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
                Assert.Throws<ArgumentOutOfRangeException>(() => Target.CreateShortLivedApiKey(Expiration, Policy, It.IsAny<string>(), out var plaintextApiKey));
            }

            public FederatedCredentialPolicy Policy { get; }

            public TheCreateShortLivedApiKeyMethod()
            {
                Policy = new FederatedCredentialPolicy
                {
                    Key = 23,
                    PackageOwner = new User { Key = 42 },
                    PackageOwnerUserKey = 42,
                    CreatedBy = new User { Key = 43 },
                    CreatedByUserKey = 43,
                };
            }
        }

        public TimeSpan Expiration { get; set; }

        public CredentialBuilder Target { get; }

        public CredentialBuilderFacts()
        {
            Expiration = TimeSpan.FromMinutes(15);

            Target = new CredentialBuilder();
        }
    }
}
