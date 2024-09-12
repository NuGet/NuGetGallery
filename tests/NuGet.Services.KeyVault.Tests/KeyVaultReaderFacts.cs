// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class SecretReaderFacts
    {
        [Fact]
        public void GetSecretObjectWithSendX5c()
        {
            // Arrange
            const string vaultName = "vaultName";
            const string tenantId = "tenantId";
            const string clientId = "clientId";
            const string secret = "secretvalue";

            X509Certificate2 certificate = new X509Certificate2();

            KeyVaultConfiguration keyVaultConfiguration = new KeyVaultConfiguration("vaultName", "tenantId", "clientId", certificate, sendX5c:true);

            // Act
            var keyvaultReader = new KeyVaultReader(keyVaultConfiguration);

            // Assert
            Assert.True(keyvaultReader.isUsingSendX5c);
        }
    }
}