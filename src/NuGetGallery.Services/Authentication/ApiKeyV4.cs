// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class ApiKeyV4
    {
        private const int IdPartLengthBytes = 10;
        private static readonly byte[] IdPrefix = Encoding.ASCII.GetBytes("v4");

        internal const int IdPartBase32Length = 20;
        internal const int IdAndPasswordLength = 46;
        internal const int IdAndPasswordHashedLength = 124;

        /// <summary>
        /// Plaintext format of the ApiKey
        /// </summary>
        public string PlaintextApiKey { get; private set; }

        /// <summary>
        /// Hashed format of the ApiKey. Will be set only if Create() method was used.
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

        private ApiKeyV4()
        {
        }

        /// <summary>
        /// Creates a random ApiKey V4.
        /// </summary>
        public static ApiKeyV4 Create()
        {
            var apiKey = new ApiKeyV4();
            apiKey.CreateInternal();

            return apiKey;
        }

        /// <summary>
        /// Parses the provided string and creates an ApiKeyV4 if it's successful.
        /// </summary>
        public static bool TryParse(string plaintextApiKey, out ApiKeyV4 apiKey)
        {
            apiKey = new ApiKeyV4();
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

            string hashedApiKeyIdPart = hashedApiKey.Substring(0, IdPartBase32Length);
            string hashedApiKeyPasswordPart = hashedApiKey.Substring(IdPartBase32Length);

            if (!string.Equals(IdPart, Normalize(hashedApiKeyIdPart)))
            {
                return false;
            }

            // The verification is not case sensitive. This is to maintain the existing behavior that ApiKey authentication is not case-sensitive.
            return V3Hasher.VerifyHash(hashedApiKeyPasswordPart.ToUpper().FromBase32String(), PasswordPart);
        }

        private void CreateInternal()
        {
            // Create Id
            var randomBytes = new byte[IdPartLengthBytes];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetNonZeroBytes(randomBytes);
            }

            byte[] idBytes = new byte[IdPartLengthBytes + IdPrefix.Length];
            Buffer.BlockCopy(src: IdPrefix, srcOffset: 0, dst: idBytes, dstOffset: 0, count: IdPrefix.Length);
            Buffer.BlockCopy(src: randomBytes, srcOffset: 0, dst: idBytes, dstOffset: IdPrefix.Length, count: randomBytes.Length);

            // Convert to Base32 string. The length of the string is APIKeyV4_IdPartBase64Length
            string idString = idBytes.ToBase32String().RemoveBase32Padding();

            // Create password
            var passwordString = Guid.NewGuid().ToByteArray().ToBase32String().RemoveBase32Padding();
            passwordString = Normalize(passwordString);

            // No need to remove padding or normalize here.. it's stored in the DB and doesn't need to be pretty
            var hashedPasswordString = V3Hasher.GenerateHashAsBytes(passwordString).ToBase32String();

            IdPart = Normalize(idString);
            PasswordPart = passwordString;
            PlaintextApiKey = IdPart + passwordString;
            HashedApiKey = IdPart + hashedPasswordString;
        }

        private bool TryParseInternal(string plaintextApiKey)
        {
            if (string.IsNullOrEmpty(plaintextApiKey) || plaintextApiKey.Length != IdAndPasswordLength)
            {
                return false;
            }

            try
            {
                var id = plaintextApiKey
                    .Substring(0, IdPartBase32Length)
                    .AppendBase32Padding()
                    .ToUpper();

                if (!id.TryParseBase32String(out var idBytes))
                {
                    return false;
                }

                bool success = idBytes[0] == IdPrefix[0] && idBytes[1] == IdPrefix[1];

                if (success)
                {
                    string password = plaintextApiKey.Substring(IdPartBase32Length);

                    PlaintextApiKey = plaintextApiKey;
                    IdPart = Normalize(id);
                    PasswordPart = Normalize(password);
                }

                return success;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private string Normalize(string input)
        {
            return input.ToLowerInvariant();
        }
    }
}