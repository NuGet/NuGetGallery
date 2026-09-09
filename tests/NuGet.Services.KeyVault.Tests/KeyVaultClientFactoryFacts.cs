// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class KeyVaultClientFactoryFacts
    {
        [Fact]
        public void CreatesClientsForConfiguredVault()
        {
            var target = new KeyVaultClientFactory(
                new KeyVaultConfiguration(
                    "test-vault",
                    "tenant-id",
                    "client-id",
                    new X509Certificate2()),
                CreateCredentialFactory());

            Assert.Equal(new Uri("https://test-vault.vault.azure.net/"), target.VaultUri);
            Assert.Equal(target.VaultUri, target.CreateSecretClient().VaultUri);
            Assert.Equal(target.VaultUri, target.CreateKeyClient().VaultUri);
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("starts--hyphens")]
        [InlineData("contains.period")]
        [InlineData("1starts-with-number")]
        [InlineData("ends-with-hyphen-")]
        [InlineData("contains@userinfo")]
        public void RejectsInvalidVaultNames(string vaultName)
        {
            Assert.Throws<ArgumentException>(
                () => new KeyVaultClientFactory(
                    new KeyVaultConfiguration(
                        vaultName,
                        "tenant-id",
                        "client-id",
                        new X509Certificate2()),
                    CreateCredentialFactory()));
        }

        [Theory]
        [InlineData("http://test-vault.vault.azure.net/keys/key/version")]
        [InlineData("https://other-vault.vault.azure.net/keys/key/version")]
        [InlineData("https://test-vault.vault.azure.net/secrets/key/version")]
        public void RejectsUnexpectedCryptographyClientUris(string keyIdentifier)
        {
            var target = new KeyVaultClientFactory(
                new KeyVaultConfiguration(
                    "test-vault",
                    "tenant-id",
                    "client-id",
                    new X509Certificate2()),
                CreateCredentialFactory());

            Assert.Throws<ArgumentException>(
                () => target.CreateCryptographyClient(new Uri(keyIdentifier)));
        }

        private static KeyVaultCredentialFactory CreateCredentialFactory()
        {
            var credential = new TestTokenCredential();
            return new KeyVaultCredentialFactory(
                useDebugCredential: false,
                createDefaultCredential: () => credential,
                createSystemAssignedCredential: () => credential,
                createUserAssignedCredential: _ => credential,
                createCertificateCredential: (_, __, ___, ____) => credential);
        }

        private sealed class TestTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return new AccessToken("token", DateTimeOffset.MaxValue);
            }

            public override ValueTask<AccessToken> GetTokenAsync(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
            }
        }
    }
}
