// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration.SecretReader;
using NuGetGallery.Diagnostics;
using Xunit;
using System.Threading;

namespace NuGetGallery.App_Start
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

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object, new DiagnosticsService(), int.MaxValue);
            
            // Act
            string value = await cachingSecretReader.GetSecretAsync("secretname");
            value = await cachingSecretReader.GetSecretAsync("secretname");

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(secret, value);
        }

        [Fact]
        public async Task WhenGetSecretIsCalledCacheIsRefreshedIfPastInterval()
        {
            // Arrange
            const string firstSecret = "secret1";
            const string secondSecret = "secret2";
            const int refreshIntervalSec = 1;
            const int delayBeforeRefreshingMs = (refreshIntervalSec + 1) * 1000;

            var mockSecretReader = new Mock<ISecretReader>();
            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>())).Returns(Task.FromResult(firstSecret));

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object, new DiagnosticsService(), refreshIntervalSec);

            // Act
            string value1 = await cachingSecretReader.GetSecretAsync("secretname");
            value1 = await cachingSecretReader.GetSecretAsync("secretname");

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(firstSecret, value1);

            // Wait and reconfigure the mockSecretReader and then retry
            Thread.Sleep(delayBeforeRefreshingMs);
            mockSecretReader.Setup(x => x.GetSecretAsync(It.IsAny<string>())).Returns(Task.FromResult(secondSecret));

            // Act 2
            string value2 = await cachingSecretReader.GetSecretAsync("secretname");
            value2 = await cachingSecretReader.GetSecretAsync("secretname");

            // Assert 2
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(secondSecret, value2);
        }
    }
}
