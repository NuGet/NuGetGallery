// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Helpers;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Authentication
{
    /// <summary>
    /// Builds all kinds of supported credentials for test purposes.
    /// </summary>
    public static class TestCredentialHelper
    {
        public static Credential CreatePbkdf2Password(string plaintextPassword)
        {
            return new Credential(
                CredentialTypes.Password.Pbkdf2,
                Crypto.HashPassword(plaintextPassword));
        }

        public static Credential CreateSha1Password(string plaintextPassword)
        {
            return new Credential(
                CredentialTypes.Password.Sha1,
                LegacyHasher.GenerateHash(plaintextPassword, Constants.Sha1HashAlgorithmId));
        }

        public static Credential CreateV1ApiKey(Guid apiKey, TimeSpan? expiration)
        {
            return CreateApiKey(CredentialTypes.ApiKey.V1, apiKey.ToString(), expiration);
        }

        public static Credential CreateV2ApiKey(Guid apiKey, TimeSpan? expiration)
        {
            return CreateApiKey(CredentialTypes.ApiKey.V2, apiKey.ToString(), expiration);
        }

        public static Credential CreateV4ApiKey(TimeSpan? expiration, out string plaintextApiKey)
        {
            var apiKey = ApiKeyV4.Create();

            plaintextApiKey = apiKey.PlaintextApiKey;

            return CreateApiKey(CredentialTypes.ApiKey.V4, apiKey.HashedApiKey, expiration);
        }

        public static Credential WithScopes(this Credential credential, ICollection<Scope> scopes)
        {
            credential.Scopes = scopes;
            return credential;
        }

        public static Credential WithDefaultScopes(this Credential credential)
        {
            var scopes = new[] {
                  new Scope("*", NuGetScopes.PackageUnlist),
                  new Scope("*", NuGetScopes.PackagePush),
                  new Scope("*", NuGetScopes.PackagePushVersion)
            };

            return credential.WithScopes(scopes);
        }

        public static Credential CreateV2VerificationApiKey(Guid apiKey)
        {
            return CreateApiKey(CredentialTypes.ApiKey.VerifyV1, apiKey.ToString(), TimeSpan.FromDays(1));
        }

        public static Credential CreateExternalCredential(string value, string tenantId = null)
        {
            return new Credential { Type = CredentialTypes.ExternalPrefix + "MicrosoftAccount", Value = value, TenantId = tenantId };
        }

        internal static Credential CreateApiKey(string type, string apiKey, TimeSpan? expiration)
        {
            return new Credential(type, apiKey.ToLowerInvariant(), expiration: expiration);
        }
    }
}
