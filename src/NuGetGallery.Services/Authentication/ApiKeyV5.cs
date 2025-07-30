// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Base62;
using Microsoft.Security.Utilities;

#nullable enable

namespace NuGetGallery.Infrastructure.Authentication
{
    /// <summary>
    /// A v5 API key for NuGetGallery. This API key format uses the "Highly Identifiable Secret" (HISv2) format provided
    /// Microsoft.Security.Utilities.Core package.
    ///
    /// Here is an example value:
    /// 
    ///   aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaJQQJ99ALN5Z0000LZENeS003NUGT3OrW
    ///
    /// It is broken down as follows:
    ///
    /// Example data                                          | Range | Description 
    /// ----------------------------------------------------- | ----- | ----------------------------------------------------
    /// aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  |  0-51 | Random bytes needed for a strong secret. 312 bits of entropy.
    /// JQQJ99                                                | 52-57 | HISv2 standard fixed signature 
    /// AL (A = 2024, L = December)                           | 58-59 | Allocation year and month as base62 digits, zero-indexed
    /// N                                                     |    60 | NuGet platform specifier
    /// 5                                                     |    61 | Version 5
    /// Z                                                     |    62 | Environment code, any base62 character
    /// 0000LZ (L = 21, Z = 35, 21 * 62^1 + 35 * 62^0 = 1337) | 63-68 | User ID integer as base62
    /// ENe (5th day, N = 13th hour, e = 30th minute)         | 69-71 | Allocation day, hour, and minute, zero-indexed
    /// S                                                     |    72 | API key type (S = short-lived, L = long-lived)
    /// 003 (3 * 5 = 15 minutes)                              | 73-75 | Expiration of the key, in increments of 5 minutes, encoded as base62
    /// NUGT                                                  | 76-79 | NuGet provider signature 
    /// 30rW                                                  | 80-83 | Part of a Marvin32 checksum, used for initial validation
    ///
    /// A plaintext value can be parsed into the above metadata components. The entire API key value should be
    /// considered as secret, and sensitive.
    ///
    /// The checksum can be used to do a fast pass of validation without a DB lookup. An invalid checksum means the
    /// API key is invalid. A valid checksum does NOT mean the API key is valid and a database lookup is needed.
    ///
    /// Additionally, the <see cref="AllocationTime"/> plus <see cref="Expiration"/> timespan can be used to determine
    /// if the API key is expired (barring clock skew).
    ///
    /// API keys can be rejected based on the <see cref="Environment"/> code, if it is not the expected value (e.g.
    /// a pre-production environment rejecting production API keys.
    ///
    /// The value stored in the database is base64 encoded SHA-512 hash of the plaintext API key. The corresponding
    /// database value for the example plaintext API key above is:
    ///
    ///   BWqhR33SkX0/BxG34nEZtByLp5uRz/H3lD89EDnFylq+peJ1EtGolGiUqOa44+5t0vlHd1joByP3rojeTF5scQ==
    ///
    /// The user ID is included in the API key so that a rate-limiting (i.e. throttling) layer can use the value as a
    /// rate limit key. A stable user ID value can be extracted with a simple substring starting at index 63 (0-based)
    /// and taking 6 characters.
    /// 
    /// The user ID is the package owner scope of the API key, not the user that created the API key. This allows a user
    /// to be rate-limited seperately from any organizations they are part of. In other words, the user ID is the
    /// <see cref="NuGet.Services.Entities.Scope.OwnerKey"/> for the <see cref="NuGet.Services.Entities.Credential"/>
    /// entity, not the <see cref="NuGet.Services.Entities.Credential.UserKey"/> value.
    ///
    /// Key stretching used in previous API key versions (such as <see cref="V3Hasher"/> used in <see cref="ApiKeyV4"/>)
    /// is not needed since the API key contains a sufficient amount of random data. The plaintext value is not
    /// persisted anywhere in NuGetGallery.
    ///
    /// An incoming plaintext API key can be parsed and hashed before querying the DB via
    /// <see cref="TryParse(string, out ApiKeyV5?)"/>. Internally this method validates the format of the API key and
    /// the checksum. A simple point read of the database for a matching <see cref="HashedApiKey"/> value is all that is
    /// needed for finding the matching API key record.
    /// </summary>
    public class ApiKeyV5
    {
        private const int SignatureOffset = 52;
        private const int SignatureLength = 6;
        private const int AllocationMonthOffset = 58;
        private const int PlatformOffset = 60;
        private const int UserKeyLength = 6;
        private const int ProviderOffset = 72;
        private const int ExpirationLength = 3;

        private const char PlatformPrefix = 'N'; // this is to differentiate with other platforms, such as 'A' for Azure
        private const char ApiKeyVersion = '5';
        internal const string ProviderSignature = "NUGT";

        public static class KnownEnvironments
        {
            public const char Production = 'P';
            public const char Integration = 'I';
            public const char Development = 'D';
            public const char Local = 'L';
        }

        private static readonly IReadOnlyDictionary<string, char> GalleryToApiKeyV5EnvironmentMappings = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            { ServicesConstants.DevelopmentEnvironment, KnownEnvironments.Local },
            { ServicesConstants.DevEnvironment, KnownEnvironments.Development },
            { ServicesConstants.IntEnvironment, KnownEnvironments.Integration },
            { ServicesConstants.ProdEnvironment, KnownEnvironments.Production },
        };

        public static class KnownApiKeyTypes
        {
            public const char LongLived = 'L';
            public const char ShortLived = 'S';
        }

        private ApiKeyV5(
            DateTime allocationTime,
            int userKey,
            char environment,
            char type,
            TimeSpan expiration,
            string hashedApiKey,
            string plaintextApiKey)
        {
            AllocationTime = allocationTime;
            UserKey = userKey;
            Environment = environment;
            Type = type;
            Expiration = expiration;
            HashedApiKey = hashedApiKey;
            PlaintextApiKey = plaintextApiKey;
        }

        public DateTime AllocationTime { get; }
        public int UserKey { get; }
        public char Environment { get; }
        public char Type { get; }
        public TimeSpan Expiration { get; }
        public string HashedApiKey { get; }
        public string PlaintextApiKey { get; }

        /// <summary>
        /// Creates a new v5 API key. The plaintext API key value will be available in the <see cref="PlaintextValue"/> property.
        /// </summary>
        /// <param name="allocationTime">
        /// The allocation (creation) time of the API key. This must be in UTC and the year must be 2024 or later.
        /// The expiration time of the API key will be this value plus the <paramref name="expiration"/> parameter.
        /// The allocation time must have a second and millisecond value of zero to allow a complete round trip from the encoded value.
        /// </param>
        /// <param name="userKey">
        /// The user key related to this API key.
        /// This is the user or organization that will have their rate limit impacted by the usage of this API key.
        /// </param>
        /// <param name="environment">The NuGetGallery environment that generated this API key.</param>
        /// <param name="type">The type of this API key (e.g. short-lived vs. long-lived).</param>
        /// <param name="expiration">The expiration time. This must be 366 days or shorter must be a round minute value divisible by 5.</param>
        /// <returns>The created API key.</returns>
        public static ApiKeyV5 Create(DateTime allocationTime, char environment, int userKey, char type, TimeSpan expiration)
            => Create(allocationTime, environment, userKey, type, expiration, testChar: null);

        internal static ApiKeyV5 CreateTestKey(DateTime allocationTime, char environment, int userKey, char type, TimeSpan expiration, char testChar)
            => Create(allocationTime, environment, userKey, type, expiration, testChar);

        private static ApiKeyV5 Create(DateTime allocationTime, char environment, int userKey, char type, TimeSpan expiration, char? testChar)
        {
            if (userKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userKey), $"The {nameof(userKey)} must be greater than zero.");
            }

            if (expiration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(expiration), $"The {nameof(expiration)} must be greater than zero.");
            }

            if (allocationTime.Second != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationTime), $"The {nameof(allocationTime)} must have a second value of zero.");
            }

            if (allocationTime.Millisecond != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationTime), $"The {nameof(allocationTime)} must have a millisecond value of zero.");
            }

            if (!TryParseBase62Char(environment, out _))
            {
                throw new ArgumentOutOfRangeException(nameof(environment), $"The {nameof(environment)} character must be alphanumeric.");
            }

            if (!TryParseBase62Char(type, out _))
            {
                throw new ArgumentOutOfRangeException(nameof(type), $"The {nameof(type)} character must be alphanumeric.");
            }

            string encodedUserKey = EncodeUserKey(userKey);
            string encodedTimeOfMonth = EncodeTimeOfMonth(allocationTime);
            string platformAllocation = $"{PlatformPrefix}{ApiKeyVersion}{environment}{encodedUserKey}{encodedTimeOfMonth}";

            string encodedExpiration = EncodeExpiration(expiration);
            string providerAllocation = $"{type}{encodedExpiration}";

            string plaintextApiKey = IdentifiableSecrets.GenerateCommonAnnotatedTestKey(
                randomBytes: null,
                IdentifiableSecrets.VersionTwoChecksumSeed,
                base64EncodedSignature: ProviderSignature,
                customerManagedKey: true,
                platformReserved: Convert.FromBase64String(platformAllocation),
                providerReserved: Convert.FromBase64String(providerAllocation),
                longForm: false,
                testChar: testChar,
                keyKindSignature: IdentifiableSecrets.CommonAnnotatedKeySignature[5], // JQQJ99 -> 9
                allocationTime: allocationTime);

            // The first 52 characters of an HISv2 key contains 312 bits of entropy. The rest of the API key is
            // composed of identifiers, checksum, and other supporting metadata. We will hash the entire thing and
            // store the hash in the database. We could choose to just hash the first 52 characters, but and store the
            // rest in plaintext in the DB for debugging purposes but we don't have a need for that right now.
            var hashedApiKey = HashApiKey(plaintextApiKey);

            return new ApiKeyV5(allocationTime, userKey, environment, type, expiration, hashedApiKey, plaintextApiKey);
        }

        /// <summary>
        /// Parses a plaintext API key into an <see cref="ApiKeyV5"/> instance. The plaintext API key must be a valid v5 API key
        /// otherwise this method will return false.
        /// </summary>
        /// <param name="plaintextApiKey">The plaintext API key.</param>
        /// <param name="parsed">The parsed API key with extracted metadata.</param>
        /// <returns>True if the API key could be parsed, false otherwise.</returns>
        public static bool TryParse(string plaintextApiKey, out ApiKeyV5? parsed)
        {
            parsed = null;

            if (plaintextApiKey.Length != IdentifiableSecrets.StandardEncodedCommonAnnotatedKeySize)
            {
                return false;
            }

            if (plaintextApiKey.Substring(SignatureOffset, SignatureLength) != IdentifiableSecrets.CommonAnnotatedKeySignature)
            {
                return false;
            }

            try
            {
                if (!IdentifiableSecrets.TryValidateCommonAnnotatedKey(plaintextApiKey, ProviderSignature))
                {
                    return false;
                }
            }
            catch (FormatException)
            {
                return false;
            }

            if (plaintextApiKey[PlatformOffset] != PlatformPrefix)
            {
                return false;
            }

            if (plaintextApiKey[PlatformOffset + 1] != ApiKeyVersion)
            {
                return false;
            }

            char environment = plaintextApiKey[PlatformOffset + 2];
            if (!TryParseBase62Char(environment, out _))
            {
                return false;
            }

            char type = plaintextApiKey[ProviderOffset];
            if (!TryParseBase62Char(type, out _))
            {
                return false;
            }

            if (!TryParseAllocationTime(plaintextApiKey, out var allocationTime))
            {
                return false;
            }

            if (!TryParseUserKey(plaintextApiKey, out var userKey))
            {
                return false;
            }

            if (!TryParseExpiration(plaintextApiKey, out var expiration))
            {
                return false;
            }

            parsed = new ApiKeyV5(
                allocationTime,
                userKey,
                environment,
                type,
                expiration,
                HashApiKey(plaintextApiKey),
                plaintextApiKey);

            return true;
        }

        public static bool TryParseAndValidate(string plaintextApiKey, char environment, out ApiKeyV5? apiKeyV5)
        {
            apiKeyV5 = null;
            if (!TryParse(plaintextApiKey, out var parsedKey))
            {
                return false;
            }

            if (parsedKey == null)
            {
                return false;
            }

            if (parsedKey.Environment != environment)
            {
                return false;
            }

            if (parsedKey.AllocationTime == null || parsedKey.Expiration == null ||
                parsedKey.AllocationTime.Add(parsedKey.Expiration) < DateTime.UtcNow)
            {
                return false;
            }

            apiKeyV5 = parsedKey;

            return true;
        }

        public static char GetEnvironment(string galleryEnvironment)
        {
            var environment = KnownEnvironments.Local;
            if (GalleryToApiKeyV5EnvironmentMappings.TryGetValue(galleryEnvironment, out var value))
            {
                environment = value;
            }

            return environment;
        }

        private static bool TryParseAllocationTime(string plaintextApiKey, out DateTime allocationTime)
        {
            allocationTime = default;

            try
            {
                allocationTime = new DateTime(
                    year: 2024 + ParseBase62Char(plaintextApiKey[AllocationMonthOffset]), // zero-indexed per HISv2 spec
                    month: 1 + ParseBase62Char(plaintextApiKey[AllocationMonthOffset + 1]), // zero-indexed per HISv2 spec
                    day: 1 + ParseBase62Char(plaintextApiKey[PlatformOffset + 9]), // zero-indexed base on implementation in this class
                    hour: ParseBase62Char(plaintextApiKey[PlatformOffset + 10]), // zero-indexed is fine for DateTime
                    minute: ParseBase62Char(plaintextApiKey[PlatformOffset + 11]), // zero-indexed is fine for DateTime
                    second: 0,
                    millisecond: 0,
                    DateTimeKind.Utc);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        private static bool TryParseUserKey(string plaintextApiKey, out int userKey)
        {
            userKey = default;

            var userKeyBase62 = plaintextApiKey.Substring(PlatformOffset + 3, UserKeyLength);
            byte[] userKeyBytes;
            try
            {
                userKeyBytes = userKeyBase62.FromBase62();
            }
            catch (Exception)
            {
                return false;
            }

            // ensure all but the 4 least significant bytes are 0 (little-endian)
            // this is to ensure that the user key is not padded with extra data
            if (!VerifyAllButLast4BytesAreZero(userKeyBytes))
            {
                return false;
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(userKeyBytes);
            }

            userKey = BitConverter.ToInt32(userKeyBytes, 0);
            return true;
        }

        private static bool TryParseExpiration(string plaintextApiKey, out TimeSpan expiration)
        {
            expiration = default;

            var expirationBase62 = plaintextApiKey.Substring(ProviderOffset + 1, ExpirationLength);
            byte[] expirationBytes;
            try
            {
                expirationBytes = expirationBase62.PadLeft(4, '0').FromBase62();
            }
            catch (Exception)
            {
                return false;
            }

            // ensure all but the 4 least significant bytes are 0 (little-endian)
            // this is to ensure that the expiration bytes is not padded with extra data
            if (!VerifyAllButLast4BytesAreZero(expirationBytes))
            {
                return false;
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(expirationBytes);
            }

            int expirationFiveMinutes = BitConverter.ToInt32(expirationBytes, 0);

            expiration = TimeSpan.FromMinutes(expirationFiveMinutes * 5);
            return true;
        }

        private static bool VerifyAllButLast4BytesAreZero(byte[] bytes)
        {
            for (var i = 0; i < bytes.Length - 4; i++)
            {
                if (bytes[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ParseBase62Char(char input)
        {
            if (!TryParseBase62Char(input, out var value))
            {
                throw new ArgumentException($"The character is not a valid base62 character.", nameof(input));
            }

            return value;
        }

        private static bool TryParseBase62Char(char input, out int value)
        {
            value = default;

            if (input >= 'A' && input <= 'Z')
            {
                value = input - 'A';
                return true;
            }

            if (input >= 'a' && input <= 'z')
            {
                value = 26 + (input - 'a');
                return true;
            }

            if (input >= '0' && input <= '9')
            {
                value = 52 + (input - '0');
                return true;
            }

            return false;
        }

        private static string HashApiKey(string plaintextApiKey)
        {
            using var hasher = SHA512.Create();
            var hash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(plaintextApiKey)));
            return hash;
        }

        private static string EncodeUserKey(int userKey)
        {
            byte[] userKeyBytes = BitConverter.GetBytes(userKey);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(userKeyBytes);
            }

            // Pad out the base62 encoded string to 6 characters with a zero (zero bits).
            // This allows the string to be interpreted as base64 with no needed padding.
            string userKeyBase62Padded = userKeyBytes.ToBase62().PadLeft(UserKeyLength, '0');
            return userKeyBase62Padded;
        }

        private static string EncodeTimeOfMonth(DateTime allocationTime)
        {
            byte zeroIndexedDay = (byte)(allocationTime.Day - 1); // zero-indexed to be consistent with HISv2 year and month
            byte hour = (byte)allocationTime.Hour;
            byte minute = (byte)allocationTime.Minute;
            int? timeOfMonthPacked = zeroIndexedDay << 12 | hour << 6 | minute;
            byte[] timeOfMonthBytes = BitConverter.GetBytes(timeOfMonthPacked.Value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timeOfMonthBytes);
            }

            // The first byte of the integer is completely unused. Start at index 1 and take the remaining 3 bytes of the 32-bit integer
            // The first 6 bits of the remaining 3 bytes are unused. 3 bytes becomes 4 base64 characters, so we can skip the first.
            string timeOfMonthEncoded = Convert.ToBase64String(timeOfMonthBytes, 1, 3).Substring(1, 3);
            return timeOfMonthEncoded;
        }

        private static string EncodeExpiration(TimeSpan expiration)
        {
            // We encode the expiration as a base62 string. The integer we encode is the total minutes of the expiration divided by 5.
            // We can only use 3 ASCII characters to encode the expiration, so the integer value must be less than 62^3. We could support
            // up to about 827 days but our long lived API keys only last up to a year so that's not needed.
            if (expiration > TimeSpan.FromDays(366))
            {
                throw new ArgumentOutOfRangeException(nameof(expiration), $"The {nameof(expiration)} must be 366 days or shorter.");
            }

            if (expiration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(expiration), $"The {nameof(expiration)} must be greater than zero.");
            }

            int expirationMinutes = (int)expiration.TotalMinutes;
            if (expiration.Ticks % TimeSpan.TicksPerMinute != 0
                || expirationMinutes % 5 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expiration), $"The total minutes of the {nameof(expiration)} must be a whole number and be a multiple of five.");
            }

            byte[] expirationBytes = BitConverter.GetBytes(expirationMinutes / 5);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(expirationBytes);
            }

            string encodedExpiration = expirationBytes.ToBase62().PadLeft(4, '0');
            if (encodedExpiration.Length > 4 || encodedExpiration[0] != '0')
            {
                throw new ArgumentOutOfRangeException(nameof(expiration), "The expiration is too long to encode.");
            }

            return encodedExpiration.Substring(1);
        }
    }
}
