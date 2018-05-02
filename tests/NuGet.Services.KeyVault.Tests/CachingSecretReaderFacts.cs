// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class CachingSecretReaderFacts
    {
        [Fact]
        public async Task WhenGetSecretIsCalledCacheIsUsed()
        {
            // Arrange
            const string secret = "secret";
            var mockSecretReader = new Mock<ISecretReader>();
            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>())).Returns(Task.FromResult(secret));

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object, int.MaxValue);

            // Act
            var value1 = await cachingSecretReader.GetSecretAsync("secretname");
            var value2 = await cachingSecretReader.GetSecretAsync("secretname");

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(secret, value1);
            Assert.Equal(value1, value2);
        }

        [Fact]
        public async Task WhenGetSecretIsCalledCacheIsRefreshedIfPastInterval()
        {
            // Arrange
            const string secretName = "secretname";
            const string firstSecret = "secret1";
            const string secondSecret = "secret2";
            const int refreshIntervalSec = 1;

            var mockSecretReader = new Mock<ISecretReader>();

            mockSecretReader
                .SetupSequence(x => x.GetSecretAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(firstSecret))
                .Returns(Task.FromResult(secondSecret));

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object, refreshIntervalSec);

            // Act
            var firstValue1 = await cachingSecretReader.GetSecretAsync(secretName);
            var firstValue2 = await cachingSecretReader.GetSecretAsync(secretName);

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(firstSecret, firstValue1);
            Assert.Equal(firstSecret, firstValue2);

            // Arrange 2
            // We are now x seconds later after refreshIntervalSec has passed.
            await Task.Delay(TimeSpan.FromSeconds(refreshIntervalSec * 2));

            // Act 2
            var secondValue1 = await cachingSecretReader.GetSecretAsync(secretName);
            var secondValue2 = await cachingSecretReader.GetSecretAsync(secretName);

            // Assert 2
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(secondSecret, secondValue1);
            Assert.Equal(secondSecret, secondValue2);
        }
    }
}