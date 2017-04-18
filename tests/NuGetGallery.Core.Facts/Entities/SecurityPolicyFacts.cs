// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Entities
{
    public class SecurityPolicyFacts
    {
        [Theory]
        [InlineData("{\"v\":\"4.1.0\"}", "4.1.0")]
        public void MinClientVersionForPushSecurityPolicy_ValueDeserializedCorrectly(string jsonValue, string minVersion)
        {
            // Arrange
            var expected = Version.Parse(minVersion);
            var policy = new PackageVerificationKeysPolicy();

            // Act
            policy.Value = jsonValue;

            // Assert
            Assert.Equal(expected, policy.MinClientVersion);
        }

        [Theory]
        [InlineData("4.1.0", "{\"v\":\"4.1.0\"}")]
        public void MinClientVersionForPushSecurityPolicy_ValueSerializedCorrectly(string minVersion, string jsonValue)
        {
            // Arrange
            var policy = new PackageVerificationKeysPolicy();

            // Act
            policy.MinClientVersion = Version.Parse(minVersion);

            // Assert
            Assert.Equal(jsonValue, policy.Value);
        }
    }
}
