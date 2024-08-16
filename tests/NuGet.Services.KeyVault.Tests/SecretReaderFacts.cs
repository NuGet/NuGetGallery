// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class SecretReaderFacts
    {
        [Fact]
        public async Task GetSecretObjectAsyncReturnsSecretExpiry()
        {
            // Arrange
            const string secretName = "secretname";
            const string secretValue = "testValue";
            DateTime secretExpiration = DateTime.UtcNow.AddSeconds(3);
            KeyVaultSecret secret = new KeyVaultSecret(secretName, secretValue, secretExpiration);

            var mockSecretReader = new Mock<ISecretReader>();
            mockSecretReader
                .SetupSequence(x => x.GetSecretObjectAsync(It.IsAny<string>()))
                .Returns(Task.FromResult((ISecret)secret));

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object);

            // Act
            var secretObject = await cachingSecretReader.GetSecretObjectAsync(secretName);

            // Assert
            mockSecretReader.Verify(x => x.GetSecretObjectAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(secretValue, secretObject.Value);
            Assert.Equal(secretObject.Expiration, secretExpiration);
        }
    }
}
