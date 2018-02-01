// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class ApiKeyV3
    {
        private const int IdPartLength = 10;
        internal const int IdAndPasswordHashedLength = 94;

        /// <summary>
        /// Plaintext format of the ApiKey
        /// </summary>
        public string PlaintextApiKey { get; private set; }

        /// <summary>
        /// Hashed format of the ApiKey. Will be set only if CreateFromV1V2ApiKey method was used.
        /// </summary>
        public string HashedApiKey { get; private set; }

        /// <summary>
        /// Id part of the ApiKey
        /// </summary>
        public string IdPart { get; private set; }

        /// <summary>
        /// Password part of the ApiKey (plaintext)
        /// </summary>
        public string PasswordPart { get; private set; }

        private ApiKeyV3()
        {
        }

        /// <summary>
        /// Creates an ApiKeyV3 from an APIKey V1/V2 format (GUID).
        /// </summary>
        public static ApiKeyV3 CreateFromV1V2ApiKey(string plaintextApiKey)
        {
            // Since V1/V2/V3 have the same format (Guid), we can use the same parse method
            if (!TryParse(plaintextApiKey, out ApiKeyV3 apiKeyV3))
            {
                throw new ArgumentException("Invalid format for ApiKey V1/V2");
            }
            
            apiKeyV3.HashedApiKey = apiKeyV3.IdPart + V3Hasher.GenerateHash(apiKeyV3.PasswordPart);

            return apiKeyV3;
        }

        /// <summary>
        /// Parses the provided string and creates an ApiKeyV3 if it's successful.
        /// The plaintext string is expected to be a GUID.
        /// </summary>
        public static bool TryParse(string plaintextApiKey, out ApiKeyV3 apiKey)
        {
            apiKey = new ApiKeyV3();
            return apiKey.TryParseInternal(plaintextApiKey);
        }

        /// <summary>
        /// Verified this ApiKey with provided hashed ApiKey. 
        /// </summary>
        public bool Verify(string hashedApiKey)
        {
            if (string.IsNullOrWhiteSpace(hashedApiKey) || hashedApiKey.Length != IdAndPasswordHashedLength)
            {
                return false;
            }

            string hashedApiKeyIdPart = hashedApiKey.Substring(0, IdPartLength);
            string hashedApiKeyPasswordPart = hashedApiKey.Substring(IdPartLength);

            if (!string.Equals(IdPart, Normalize(hashedApiKeyIdPart)))
            {
                return false;
            }

            return V3Hasher.VerifyHash(hashedApiKeyPasswordPart, PasswordPart);
        }

        private bool TryParseInternal(string plaintextApiKey)
        {
            if (!Guid.TryParse(plaintextApiKey, out Guid apiKeyGuid))
            {
                return false;
            }

            var apiKeyString = apiKeyGuid.ToString("N");

            IdPart = Normalize(apiKeyString.Substring(0, IdPartLength));
            PasswordPart = apiKeyString.Substring(IdPartLength);
            PlaintextApiKey = Normalize(apiKeyGuid.ToString());

            return true;
        }

        private static string Normalize(string input)
        {
            return input.ToLowerInvariant();
        }
    }
}