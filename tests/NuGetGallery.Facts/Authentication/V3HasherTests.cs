// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class V3HasherTests
    {
        [Fact]
        public void WhenStringIsHashedItCanBeVerified()
        {
            // Arrange
            string password = "arew234235wfsdq2321edfewt";

            // Act
            string hash = V3Hasher.GenerateHash(password);

            // Assert
            Assert.True(V3Hasher.VerifyHash(hash, password));
        }

        [Fact]
        public void WhenWrongStringIsVerifiedThenVerificationFails()
        {
            // Arrange
            string password = "arew234235wfsdq2321edfewt";
            string hash = V3Hasher.GenerateHash(password);

            // Act
            bool verify = V3Hasher.VerifyHash(hash, password+"1");

            // Assert
            Assert.False(verify);
        }
    }
}
