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
    public class KeyVaultCredentialFactoryFacts
    {
        [Fact]
        public void UsesDefaultCredentialForLocalDevelopment()
        {
            var credentials = new TestCredentials();
            KeyVaultCredentialFactory target = credentials.CreateFactory(useDebugCredential: false);

            TokenCredential actual = target.CreateCredential(
                new KeyVaultConfiguration("test-vault", clientId: "client-id", localDevelopment: true));

            Assert.Same(credentials.Default, actual);
        }

        [Fact]
        public void UsesDefaultCredentialForDebugBuilds()
        {
            var credentials = new TestCredentials();
            KeyVaultCredentialFactory target = credentials.CreateFactory(useDebugCredential: true);

            TokenCredential actual = target.CreateCredential(
                new KeyVaultConfiguration("test-vault", clientId: "client-id"));

            Assert.Same(credentials.Default, actual);
        }

        [Fact]
        public void UsesSystemAssignedManagedIdentityWithoutClientId()
        {
            var credentials = new TestCredentials();
            KeyVaultCredentialFactory target = credentials.CreateFactory(useDebugCredential: false);

            TokenCredential actual = target.CreateCredential(new KeyVaultConfiguration("test-vault"));

            Assert.Same(credentials.SystemAssigned, actual);
        }

        [Fact]
        public void UsesUserAssignedManagedIdentityWithClientId()
        {
            var credentials = new TestCredentials();
            KeyVaultCredentialFactory target = credentials.CreateFactory(useDebugCredential: false);

            TokenCredential actual = target.CreateCredential(
                new KeyVaultConfiguration("test-vault", clientId: "client-id"));

            Assert.Same(credentials.UserAssigned, actual);
            Assert.Equal("client-id", credentials.UserAssignedClientId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UsesCertificateCredentialWithSendX5cSetting(bool sendX5c)
        {
            var credentials = new TestCredentials();
            KeyVaultCredentialFactory target = credentials.CreateFactory(useDebugCredential: false);
            var certificate = new X509Certificate2();

            TokenCredential actual = target.CreateCredential(
                new KeyVaultConfiguration(
                    "test-vault",
                    tenantId: "tenant-id",
                    clientId: "client-id",
                    certificate,
                    sendX5c));

            Assert.Same(credentials.Certificate, actual);
            Assert.Equal("tenant-id", credentials.TenantId);
            Assert.Equal("client-id", credentials.CertificateClientId);
            Assert.Same(certificate, credentials.X509Certificate);
            Assert.Equal(sendX5c, credentials.SendX5c);
        }

        private sealed class TestCredentials
        {
            public TokenCredential Default { get; } = new TestTokenCredential();
            public TokenCredential SystemAssigned { get; } = new TestTokenCredential();
            public TokenCredential UserAssigned { get; } = new TestTokenCredential();
            public TokenCredential Certificate { get; } = new TestTokenCredential();
            public string UserAssignedClientId { get; private set; }
            public string TenantId { get; private set; }
            public string CertificateClientId { get; private set; }
            public X509Certificate2 X509Certificate { get; private set; }
            public bool SendX5c { get; private set; }

            public KeyVaultCredentialFactory CreateFactory(bool useDebugCredential)
            {
                return new KeyVaultCredentialFactory(
                    useDebugCredential,
                    () => Default,
                    () => SystemAssigned,
                    clientId =>
                    {
                        UserAssignedClientId = clientId;
                        return UserAssigned;
                    },
                    (tenantId, clientId, certificate, sendX5c) =>
                    {
                        TenantId = tenantId;
                        CertificateClientId = clientId;
                        X509Certificate = certificate;
                        SendX5c = sendX5c;
                        return Certificate;
                    });
            }
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
