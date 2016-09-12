// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Helpers;
using NuGetGallery.Services.Authentication;

namespace NuGetGallery.Authentication
{
    /// <summary>
    /// Builds all kinds of supported credentials for test puposes.
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
            return CreateV1ApiKey(apiKey.ToString(), expiration);
        }

        internal static Credential CreateV1ApiKey(string apiKey, TimeSpan? expiration)
        {
            return new Credential(CredentialTypes.ApiKeyV1, apiKey.ToLowerInvariant(), expiration: expiration);
        }
    }
}
