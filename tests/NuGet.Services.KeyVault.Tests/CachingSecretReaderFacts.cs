// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using Xunit;
using System.Threading;

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

        [Theory]
        // Secret was refreshed more recently than the refresh interval and is not outdated.
        [InlineData(2, 1, false)]
        // Secret was refreshed after the refresh interval is outdated
        [InlineData(2, 3, true)]
        // Secret was refreshed exactly on the refresh interval and is outdated.
        [InlineData(2, 2, true)]
        public void CorrectlyIdentifiesOutdatedSecrets(int refreshIntervalSec, int secretLastRefreshedSec, bool isOutdated)
        {
            // Arrange
            var cachingSecretReaderMock = new Mock<CachingSecretReader>(new Mock<ISecretReader>().Object, refreshIntervalSec) {CallBase = true};
            var secretToCheck = Tuple.Create("secretName",
                DateTime.UtcNow.Add(new TimeSpan(0, 0, -secretLastRefreshedSec)));

            // Act
            var result = cachingSecretReaderMock.Object.IsSecretOutdated(secretToCheck);

            // Assert
            Assert.Equal(isOutdated, result);
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
            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>())).Returns(Task.FromResult(firstSecret));

            var cachingSecretReaderMock = new Mock<CachingSecretReader>(mockSecretReader.Object, refreshIntervalSec)
            {
                CallBase = true
            };

            var hasIntervalPassed = false;
            cachingSecretReaderMock.Setup(x => x.IsSecretOutdated(It.IsAny<Tuple<string, DateTime>>())).Returns(() =>
            {
                // If the interval hasn't passed, the secret we have stored is not outdated.
                if (!hasIntervalPassed)
                {
                    return false;
                }

                // If the interval has passed, the secret is outdated.
                // It will be refreshed and then the interval will not have passed again.
                hasIntervalPassed = false;
                return true;
            });

            // Act
            var firstValue1 = await cachingSecretReaderMock.Object.GetSecretAsync(secretName);
            var firstValue2 = await cachingSecretReaderMock.Object.GetSecretAsync(secretName);

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(firstSecret, firstValue1);
            Assert.Equal(firstValue1, firstValue2);

            // Arrange 2
            // We are now x seconds later after refreshIntervalSec has passed.
            hasIntervalPassed = true;
            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>())).Returns(Task.FromResult(secondSecret));

            // Act 2
            var secondValue1 = await cachingSecretReaderMock.Object.GetSecretAsync(secretName);
            var secondValue2 = await cachingSecretReaderMock.Object.GetSecretAsync(secretName);

            // Assert 2
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(secondSecret, secondValue1);
            Assert.Equal(secondValue1, secondValue2);
        }
    }
}
