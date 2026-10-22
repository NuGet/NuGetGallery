// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery
{
    public class ApiKeyReductionPolicyFacts
    {
        public class TheGetEffectiveExpirationMethod
        {
            private static readonly DateTime Cutoff = ApiKeyReductionPolicy.CutoffUtc;

            [Fact]
            public void CapsLongDurationKeyThatExpiresAfterCutoff()
            {
                // Arrange
                var created = Cutoff.AddDays(-100);
                var expires = Cutoff.AddDays(200);

                // Act
                var result = ApiKeyReductionPolicy.GetEffectiveExpiration(created, expires);

                // Assert
                Assert.Equal(Cutoff, result);
            }

            [Fact]
            public void DoesNotCapWhenDurationIsThirtyDaysOrLess()
            {
                // Arrange - duration exactly 30 days, but expiring after the cutoff.
                var created = Cutoff.AddDays(10);
                var expires = created.AddDays(30);

                // Act
                var result = ApiKeyReductionPolicy.GetEffectiveExpiration(created, expires);

                // Assert
                Assert.Equal(expires, result);
            }

            [Fact]
            public void DoesNotCapWhenExpirationIsOnOrBeforeCutoff()
            {
                // Arrange - long duration but expires before the cutoff.
                var created = Cutoff.AddDays(-200);
                var expires = Cutoff.AddDays(-1);

                // Act
                var result = ApiKeyReductionPolicy.GetEffectiveExpiration(created, expires);

                // Assert
                Assert.Equal(expires, result);
            }

            [Fact]
            public void ReturnsNullWhenNoExpiration()
            {
                // Arrange
                var created = Cutoff.AddDays(-200);

                // Act
                var result = ApiKeyReductionPolicy.GetEffectiveExpiration(created, expires: null);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void CapsWhenDurationIsJustOverThirtyDays()
            {
                // Arrange - duration slightly over 30 days and expiring after the cutoff.
                var created = Cutoff.AddDays(-1);
                var expires = Cutoff.AddDays(30);

                // Act
                var result = ApiKeyReductionPolicy.GetEffectiveExpiration(created, expires);

                // Assert
                Assert.Equal(Cutoff, result);
            }
        }
    }
}
