// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Moq;
using Xunit;

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
    }
}