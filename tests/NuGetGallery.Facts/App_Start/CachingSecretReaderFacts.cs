// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration.SecretReader;
using Xunit;

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

            var cachingSecretReader = new CachingSecretReader(mockSecretReader.Object);
            
            // Act
            string value = await cachingSecretReader.GetSecretAsync("secretname");
            value = await cachingSecretReader.GetSecretAsync("secretname");

            // Assert
            mockSecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(secret, value);
        }
    }
}
