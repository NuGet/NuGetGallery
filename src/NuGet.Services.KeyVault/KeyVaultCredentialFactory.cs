// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultCredentialFactory
    {
        private readonly bool _useDebugCredential;
        private readonly Func<TokenCredential> _createDefaultCredential;
        private readonly Func<TokenCredential> _createSystemAssignedCredential;
        private readonly Func<string, TokenCredential> _createUserAssignedCredential;
        private readonly Func<string, string, X509Certificate2, bool, TokenCredential> _createCertificateCredential;

        public KeyVaultCredentialFactory()
            : this(
                  UseDebugCredential,
                  () => new DefaultAzureCredential(),
                  () => new ManagedIdentityCredential(),
                  clientId => new ManagedIdentityCredential(clientId),
                  CreateCertificateCredential)
        {
        }

        internal KeyVaultCredentialFactory(
            bool useDebugCredential,
            Func<TokenCredential> createDefaultCredential,
            Func<TokenCredential> createSystemAssignedCredential,
            Func<string, TokenCredential> createUserAssignedCredential,
            Func<string, string, X509Certificate2, bool, TokenCredential> createCertificateCredential)
        {
            _useDebugCredential = useDebugCredential;
            _createDefaultCredential = createDefaultCredential ?? throw new ArgumentNullException(nameof(createDefaultCredential));
            _createSystemAssignedCredential = createSystemAssignedCredential ?? throw new ArgumentNullException(nameof(createSystemAssignedCredential));
            _createUserAssignedCredential = createUserAssignedCredential ?? throw new ArgumentNullException(nameof(createUserAssignedCredential));
            _createCertificateCredential = createCertificateCredential ?? throw new ArgumentNullException(nameof(createCertificateCredential));
        }

        public TokenCredential CreateCredential(KeyVaultConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.UseManagedIdentity)
            {
                if (_useDebugCredential || configuration.LocalDevelopment)
                {
                    return _createDefaultCredential();
                }

                if (string.IsNullOrWhiteSpace(configuration.ClientId))
                {
                    return _createSystemAssignedCredential();
                }

                return _createUserAssignedCredential(configuration.ClientId);
            }

            return _createCertificateCredential(
                configuration.TenantId,
                configuration.ClientId,
                configuration.Certificate,
                configuration.SendX5c);
        }

        private static TokenCredential CreateCertificateCredential(
            string tenantId,
            string clientId,
            X509Certificate2 certificate,
            bool sendX5c)
        {
            if (!sendX5c)
            {
                return new ClientCertificateCredential(tenantId, clientId, certificate);
            }

            return new ClientCertificateCredential(
                tenantId,
                clientId,
                certificate,
                new ClientCertificateCredentialOptions
                {
                    SendCertificateChain = true,
                });
        }

        private static bool UseDebugCredential
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }
    }
}
