// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class CredentialBuilder : ICredentialBuilder
    {
        public const string LatestPasswordType = CredentialTypes.Password.V3;

        public Credential CreatePasswordCredential(string plaintextPassword)
        {
            return new Credential(
                LatestPasswordType,
                V3Hasher.GenerateHash(plaintextPassword));
        }

        public Credential CreateApiKey(TimeSpan? expiration, out string plaintextApiKey)
        {
            var apiKey = ApiKeyV4.Create();

            plaintextApiKey = apiKey.PlaintextApiKey;

            return new Credential(
               CredentialTypes.ApiKey.V4,
               apiKey.HashedApiKey,
               expiration: expiration);
        }

        public Credential CreatePackageVerificationApiKey(Credential originalApiKey, string id)
        {
            var credential = new Credential(
               CredentialTypes.ApiKey.VerifyV1,
               CreateKeyString(),
               expiration: TimeSpan.FromDays(1));

            var ownerKeys = originalApiKey.Scopes
                .Select(s => s.OwnerKey)
                .Distinct().ToArray();

            if (ownerKeys.Length == 0)
            {
                // Legacy API key with no owner scope.
                credential.Scopes = new[] { new Scope(
                    owner: null,
                    subject: id,
                    allowedAction: NuGetScopes.PackageVerify)
                };
            }
            else
            {
                credential.Scopes = ownerKeys
                    .Select(key => new Scope(
                        ownerKey: key,
                        subject: id,
                        allowedAction: NuGetScopes.PackageVerify))
                    .ToArray();
            }

            return credential;
        }

        public Credential CreateExternalCredential(string issuer, string value, string identity, string tenantId = null)
        {
            return new Credential(CredentialTypes.External.Prefix + issuer, value)
            {
                Identity = identity,
                TenantId = tenantId
            };
        }

        private static string CreateKeyString()
        {
            return Guid.NewGuid().ToString().ToLowerInvariant();
        }
    }
}