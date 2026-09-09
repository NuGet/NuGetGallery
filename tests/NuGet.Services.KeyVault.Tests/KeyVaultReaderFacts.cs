// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AzureSecret = Azure.Security.KeyVault.Secrets.KeyVaultSecret;

namespace NuGet.Services.KeyVault.Tests
{
    public class KeyVaultReaderFacts
    {
        [Fact]
        public void VerifyKeyvaultReaderSendX5c()
        {
            // Arrange
            const string vaultName = "vaultName";
            const string tenantId = "tenantId";
            const string clientId = "clientId";

            X509Certificate2 certificate = new X509Certificate2();
            KeyVaultConfiguration keyVaultConfiguration = new KeyVaultConfiguration(vaultName, tenantId, clientId, certificate, sendX5c:true);

            var mockSecretClient = new Mock<SecretClient>();

            // Act
            var keyvaultReader = new KeyVaultReader(mockSecretClient.Object, keyVaultConfiguration, testMode: true);

            // Assert

            // The KeyVaultReader constructor is internal which accepts a SecretClient object, KeyVaultConfiguration object and a boolean testMode parameter
            // The KeyVaultConfiguration object has the sendX5c property which is set to true
            // The KeyVaultReader object has an internal boolean _isUsingSendx5c which is set to true if the sendX5c property is set to true
            // The KeyVaultReader shot-circuits when the testMode is set to true instead of calling Azure KeyVault
            Assert.True(keyvaultReader._isUsingSendx5c);
        }

        [Fact]
        public async Task PreservesAllSecretReaderMethods()
        {
            const string secretName = "secret-name";
            const string secretValue = "secret-value";
            var expiresOn = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var secret = new AzureSecret(secretName, secretValue);
            secret.Properties.ExpiresOn = expiresOn;
            var response = Mock.Of<Response>();
            var secretClient = new Mock<SecretClient>();
            secretClient
                .Setup(x => x.GetSecret(secretName, null, It.IsAny<CancellationToken>()))
                .Returns(Response.FromValue(secret, response));
            secretClient
                .Setup(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(secret, response));
            var target = new KeyVaultReader(
                secretClient.Object,
                new KeyVaultConfiguration("test-vault"),
                testMode: true);
            ILogger logger = Mock.Of<ILogger>();

            Assert.Equal(secretValue, target.GetSecret(secretName));
            Assert.Equal(secretValue, target.GetSecret(secretName, logger));
            Assert.Equal(secretValue, await target.GetSecretAsync(secretName));
            Assert.Equal(secretValue, await target.GetSecretAsync(secretName, logger));
            AssertSecret(target.GetSecretObject(secretName), secretName, secretValue, expiresOn);
            AssertSecret(target.GetSecretObject(secretName, logger), secretName, secretValue, expiresOn);
            AssertSecret(await target.GetSecretObjectAsync(secretName), secretName, secretValue, expiresOn);
            AssertSecret(await target.GetSecretObjectAsync(secretName, logger), secretName, secretValue, expiresOn);

            secretClient.Verify(
                x => x.GetSecret(secretName, null, It.IsAny<CancellationToken>()),
                Times.Exactly(4));
            secretClient.Verify(
                x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        private static void AssertSecret(
            ISecret secret,
            string expectedName,
            string expectedValue,
            DateTimeOffset expectedExpiration)
        {
            Assert.Equal(expectedName, secret.Name);
            Assert.Equal(expectedValue, secret.Value);
            Assert.Equal(expectedExpiration, secret.Expiration);
        }
    }

}