// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class UserSecurityPolicyFacts
    {
        [Fact]
        public void CtorWithPolicyCopiesProperties()
        {
            // Arrange
            var policy = new UserSecurityPolicy("A", "B", "C");

            // Act
            var copy = new UserSecurityPolicy(policy);

            // Assert
            Assert.Equal(policy.Name, copy.Name);
            Assert.Equal(policy.Subscription, copy.Subscription);
            Assert.Equal(policy.Value, copy.Value);
        }

        public static IEnumerable<object[]> EqualsReturnsTrue_Data
        {
            get
            {
                yield return new[]
                {
                    new UserSecurityPolicy("A", "B", ""), new UserSecurityPolicy("A", "B", null)
                };
                yield return new[]
                {
                    new UserSecurityPolicy("A", "B", "C"), new UserSecurityPolicy("a", "b", "c")
                };
            }
        }

        [Theory]
        [MemberData(nameof(EqualsReturnsTrue_Data))]
        public void EqualsReturnsTrueForPolicyMatches(UserSecurityPolicy first, UserSecurityPolicy second)
        {
            // Act & Assert
            Assert.True(first.Equals(second));
        }

        public static IEnumerable<object[]> EqualsReturnsFalse_Data
        {
            get
            {
                yield return new[]
                {
                    new UserSecurityPolicy("A", "B", ""), new UserSecurityPolicy("B", "B", "")
                };
                yield return new[]
                {
                    new UserSecurityPolicy("A", "B", ""), new UserSecurityPolicy("A", "A", "")
                };
                yield return new[]
                {
                    new UserSecurityPolicy("A", "B", "C"), new UserSecurityPolicy("A", "B", "Z")
                };
            }
        }

        [Theory]
        [MemberData(nameof(EqualsReturnsFalse_Data))]
        public void EqualsReturnsFalseForPolicyNonMatches(UserSecurityPolicy first, UserSecurityPolicy second)
        {
            // Act & Assert
            Assert.False(first.Equals(second));
        }
    }
}