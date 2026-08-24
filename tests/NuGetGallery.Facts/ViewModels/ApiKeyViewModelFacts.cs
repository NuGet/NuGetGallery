// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery
{
    public class ApiKeyViewModelFacts
    {
        private static CredentialViewModel CreateCredential(DateTime created, DateTime? expires)
        {
            return new CredentialViewModel
            {
                Key = 1,
                Type = CredentialTypes.ApiKey.V4,
                Created = created,
                Expires = expires,
                HasExpired = expires.HasValue && DateTime.UtcNow >= expires.Value,
                Scopes = new List<ScopeViewModel>(),
            };
        }

        /*[Fact]
        public void WhenCutoffApplied_CapsExpirationOfLongDurationKey()
        {
            // Arrange
            var created = ApiKeyReductionPolicy.CutoffUtc.AddDays(-100);
            var expires = ApiKeyReductionPolicy.CutoffUtc.AddDays(200);
            var cred = CreateCredential(created, expires);

            // Act
            var model = new ApiKeyViewModel(cred, applyApiKeyReductionCutoff: true);

            // Assert
            Assert.Equal(ApiKeyReductionPolicy.CutoffUtc.ToString("O"), model.Expires);
            Assert.False(model.HasExpired);
        }*/

        [Fact]
        public void WhenCutoffNotApplied_KeepsOriginalExpiration()
        {
            // Arrange
            var created = ApiKeyReductionPolicy.CutoffUtc.AddDays(-100);
            var expires = ApiKeyReductionPolicy.CutoffUtc.AddDays(200);
            var cred = CreateCredential(created, expires);

            // Act
            var model = new ApiKeyViewModel(cred, applyApiKeyReductionCutoff: false);

            // Assert
            Assert.Equal(expires.ToString("O"), model.Expires);
        }

        [Fact]
        public void WhenCutoffApplied_ShortDurationKeyIsUnchanged()
        {
            // Arrange - duration <= 30 days, so the cap does not apply even after the cutoff.
            var created = ApiKeyReductionPolicy.CutoffUtc.AddDays(10);
            var expires = created.AddDays(20);
            var cred = CreateCredential(created, expires);

            // Act
            var model = new ApiKeyViewModel(cred, applyApiKeyReductionCutoff: true);

            // Assert
            Assert.Equal(expires.ToString("O"), model.Expires);
        }
    }
}
