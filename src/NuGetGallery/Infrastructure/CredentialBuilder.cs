using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Provides helper methods to generate credentials.
    /// </summary>
    public static class CredentialBuilder
    {
        public static Credential CreateV1ApiKey()
        {
            return CreateV1ApiKey(Guid.NewGuid());
        }

        public static Credential CreateV1ApiKey(Guid apiKey)
        {
            return CreateV1ApiKey(apiKey.ToString());
        }

        public static Credential CreatePbkdf2Password(string plaintextPassword)
        {
            return new Credential(
                CredentialTypes.Password.Pbkdf2,
                CryptographyService.GenerateSaltedHash(plaintextPassword, Constants.PBKDF2HashAlgorithmId));
        }

        public static Credential CreateSha1Password(string plaintextPassword)
        {
            return new Credential(
                CredentialTypes.Password.Sha1,
                CryptographyService.GenerateSaltedHash(plaintextPassword, Constants.Sha1HashAlgorithmId));
        }

        internal static Credential CreateV1ApiKey(string apiKey)
        {
            return new Credential(CredentialTypes.ApiKeyV1, apiKey.ToLowerInvariant());
        }

        internal static Credential CreateExternalCredential(string issuer, string value, string identity)
        {
            return new Credential(CredentialTypes.ExternalPrefix + issuer, value)
            {
                Identity = identity
            };
        }
    }
}