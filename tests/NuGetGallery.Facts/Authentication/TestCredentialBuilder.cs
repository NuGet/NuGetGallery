// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Helpers;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Authentication
{
    /// <summary>
    /// Builds all kinds of supported credentials for test purposes.
    /// </summary>
    public class TestCredentialBuilder
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

        public static Credential CreateExternalCredential(string value)
        {
            return new Credential { Type = CredentialTypes.ExternalPrefix + "MicrosoftAccount", Value = value };
        }

        internal static Credential CreateApiKey(string type, string apiKey, TimeSpan? expiration)
        {
            return new Credential(type, apiKey.ToLowerInvariant(), expiration: expiration);
        }
    }
}
