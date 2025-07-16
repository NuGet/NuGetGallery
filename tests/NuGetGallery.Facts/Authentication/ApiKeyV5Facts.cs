// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Base62;
using Microsoft.Security.Utilities;
using Xunit;

#nullable enable

namespace NuGetGallery.Infrastructure.Authentication
{
    public class ApiKeyV5Facts
    {
        private const string TestApiKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaJQQJ99ALN5Z0000LZENeS003NUGT3OrW";
        private const string HashedTestApiKey = "BWqhR33SkX0/BxG34nEZtByLp5uRz/H3lD89EDnFylq+peJ1EtGolGiUqOa44+5t0vlHd1joByP3rojeTF5scQ==";

        public DateTime AllocationTime { get; set; }
        public char Environment { get; set; }
        public int UserKey { get; set; }
        public char Type { get; set; }
        public TimeSpan Expiration { get; set; }

        public class TheCreateMethod : ApiKeyV5Facts
        {
            [Fact]
            public void UsesAllParameters()
            {
                // Act
                var apiKey = ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration);

                // Assert
                Assert.Equal(AllocationTime, apiKey.AllocationTime);
                Assert.Equal(Environment, apiKey.Environment);
                Assert.Equal(UserKey, apiKey.UserKey);
                Assert.Equal(Type, apiKey.Type);
                Assert.Equal(Expiration, apiKey.Expiration);
            }

            [Fact]
            public void ApiKeyMatchesExpectedPatterns()
            {
                // Act
                var apiKey = ApiKeyV5.CreateTestKey(AllocationTime, Environment, UserKey, Type, Expiration, testChar: 'a');

                // Assert
                Assert.Equal(84, apiKey.PlaintextApiKey.Length);
                Assert.Contains("JQQJ99", apiKey.PlaintextApiKey, StringComparison.Ordinal);

                Assert.Equal(88, apiKey.HashedApiKey.Length); // 512 bits, base64 encoded
                Assert.Equal(512 / 8, Convert.FromBase64String(apiKey.HashedApiKey).Length); // 512 bits from a SHA-512 hash
                Assert.Contains("NUGT", apiKey.PlaintextApiKey, StringComparison.Ordinal); // our expected provider signature
                Assert.Contains(Type, apiKey.PlaintextApiKey);
                Assert.Contains(Environment, apiKey.PlaintextApiKey);
            }

            [Fact]
            public void MatchesExpectedFormat()
            {
                // Act
                var apiKey = ApiKeyV5.CreateTestKey(AllocationTime, Environment, UserKey, Type, Expiration, testChar: 'a');

                // Assert
                Assert.Equal(TestApiKey, apiKey.PlaintextApiKey);
                Assert.Equal(HashedTestApiKey, apiKey.HashedApiKey);
            }

            [Fact]
            public void EachApiKeyIsDifferent()
            {
                // Act
                var apiKeyA = ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration);
                var apiKeyB = ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration);

                // Assert
                Assert.NotEqual(apiKeyA.PlaintextApiKey, apiKeyB.PlaintextApiKey);
                Assert.NotEqual(apiKeyA.HashedApiKey, apiKeyB.HashedApiKey);
            }

            [Fact]
            public void HashIsSha512()
            {
                // Act
                var apiKey = ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration);

                // Assert
                using var sha512 = SHA512.Create();
                var hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(apiKey.PlaintextApiKey));
                var hashBase64 = Convert.ToBase64String(hashBytes);
                Assert.Equal(hashBase64, apiKey.HashedApiKey);
            }

            [Fact]
            public void RejectsNegativeExpiration()
            {
                // Arrange
                Expiration = TimeSpan.FromMinutes(-1);

                // Act & Assert
                Assert.Throws<ArgumentOutOfRangeException>(() => ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration));
            }

            [Fact]
            public void RejectsTooLongExpiration()
            {
                // Arrange
                Expiration = TimeSpan.FromDays(367);

                // Act & Assert
                Assert.Throws<ArgumentOutOfRangeException>(() => ApiKeyV5.Create(AllocationTime, Environment, UserKey, Type, Expiration));
            }
        }

        public class TheTryParseMethod : ApiKeyV5Facts
        {
            [Fact]
            public void CanParseValidKey()
            {
                // Act
                var result = ApiKeyV5.TryParse(TestApiKey, out var parsedKey);

                // Assert
                Assert.True(result);
                Assert.NotNull(parsedKey);
                Assert.Equal(TestApiKey, parsedKey.PlaintextApiKey);
                Assert.Equal(HashedTestApiKey, parsedKey.HashedApiKey);
                Assert.Equal(AllocationTime, parsedKey.AllocationTime);
                Assert.Equal(Environment, parsedKey.Environment);
                Assert.Equal(UserKey, parsedKey.UserKey);
                Assert.Equal(Type, parsedKey.Type);
                Assert.Equal(Expiration, parsedKey.Expiration);
            }

            [Fact]
            public void RejectsInvalidString()
            {
                // Act
                var result = ApiKeyV5.TryParse("invalid-key", out var parsedKey);

                // Assert
                Assert.False(result);
                Assert.Null(parsedKey);
            }

            public static IEnumerable<object[]> AllIndicies => Enumerable.Range(0, TestApiKey.Length - 1).Select(i => new object[] { i });

            [Theory]
            [MemberData(nameof(AllIndicies))]
            public void RejectsNonBase62Character(int index)
            {
                // Arrange
                var chars = TestApiKey.ToCharArray();
                chars[index] = '+';
                var apiKey = new string(chars);

                // Act
                var result = ApiKeyV5.TryParse(apiKey, out var parsedKey);

                // Assert
                Assert.False(result);
                Assert.Null(parsedKey);
            }

            [Fact]
            public void RejectsTooLongSecretWithCorrectChecksum()
            {
                // Arrange
                var prefix = "aaaaaaaa";
                var apiKey = FixChecksum(prefix + TestApiKey, dataLength: 80 + prefix.Length, validate: false);

                // Act
                var result = ApiKeyV5.TryParse(apiKey, out var parsedKey);

                // Assert
                Assert.False(result);
                Assert.Null(parsedKey);
            }

            [Fact]
            public void RejectsInvalidChecksum()
            {
                // Arrange
                var input = TestApiKey.Substring(0, TestApiKey.Length - 4) + "XXXX";

                // Act & Assert
                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidPlatformPrefix()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(60, 1).Insert(60, "A"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidApiKeyVersion()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(61, 1).Insert(61, "4"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidEnvironmentCode()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(62, 1).Insert(62, "/"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidTypeCode()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(72, 1).Insert(72, "/"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidAllocationTimeDay()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(69, 1).Insert(69, "9"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidAllocationTimeHour()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(70, 1).Insert(70, "9"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidAllocationTimeMinute()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(71, 1).Insert(71, "9"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidUserKey()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(63, 1).Insert(63, "/"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            [Fact]
            public void RejectsInvalidExpiration()
            {
                // Arrange
                var input = FixChecksum(TestApiKey.Remove(73, 1).Insert(73, "/"));

                // Act & Assert

                Assert.False(ApiKeyV5.TryParse(input, out var parsedKey));
            }

            private static string FixChecksum(string input, int dataLength = 80, bool validate = true)
            {
                // source: https://github.com/microsoft/security-utilities/blob/a98b71ccdccf35d7d45b07095b75f6f05b4de9ad/src/Microsoft.Security.Utilities.Core/IdentifiableSecrets.cs#L429-L457
                var dataBase64 = input.Substring(0, dataLength);
                var dataBytes = Convert.FromBase64String(dataBase64);
                var checksumBytes = BitConverter.GetBytes(Marvin.ComputeHash32(dataBytes, IdentifiableSecrets.VersionTwoChecksumSeed, 0, dataBytes.Length));
                var checksumBase62 = checksumBytes.ToBase62();
                var checksumBase64 = checksumBase62 + new string('0', 6 - checksumBase62.Length) + "==";
                var checksumBase64Bytes = Convert.FromBase64String(checksumBase64);
                var fixedChecksum = dataBase64 + Convert.ToBase64String(checksumBase64Bytes).Substring(0, 4);
                if (validate)
                {
                    Assert.True(IdentifiableSecrets.TryValidateCommonAnnotatedKey(fixedChecksum, ApiKeyV5.ProviderSignature), "The checksum was not fixed properly.");
                }
                return fixedChecksum;
            }
        }

        public class TheTryParseAndValidateMethod : ApiKeyV5Facts
        {
            [Fact]
            public void ValidApiKeyV5()
            {
                // Arrange
                var allocationTime = DateTime.UtcNow;
                allocationTime = allocationTime.AddSeconds(-allocationTime.Second).AddMilliseconds(-allocationTime.Millisecond);

                var apiKey = ApiKeyV5.Create(allocationTime, Environment, UserKey, Type, Expiration);

                // Act
                var result = ApiKeyV5.TryParseAndValidate(apiKey.PlaintextApiKey, Environment, out var apiKeyV5);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void InvalidApiKeyV5WithNotMatchedEnvironment()
            {
                // Arrange
                var allocationTime = DateTime.UtcNow;
                allocationTime = allocationTime.AddSeconds(-allocationTime.Second).AddMilliseconds(-allocationTime.Millisecond);

                var apiKey = ApiKeyV5.Create(allocationTime, Environment, UserKey, Type, Expiration);

                // Act
                var result = ApiKeyV5.TryParseAndValidate(TestApiKey, ApiKeyV5.KnownEnvironments.Production, out var apiKeyV5);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void InvalidhApiKeyV5WithExpired()
            {
                // Arrange & Act
                var result = ApiKeyV5.TryParseAndValidate(TestApiKey, Environment, out var apiKeyV5);

                // Assert
                Assert.False(result);
            }
        }

        public class TheGetEnvironmentMethod : ApiKeyV5Facts
        {
            [Theory]
            [InlineData(ServicesConstants.ProdEnvironment, ApiKeyV5.KnownEnvironments.Production)]
            [InlineData(ServicesConstants.IntEnvironment, ApiKeyV5.KnownEnvironments.Integration)]
            [InlineData(ServicesConstants.DevEnvironment, ApiKeyV5.KnownEnvironments.Development)]
            [InlineData(ServicesConstants.DevelopmentEnvironment, ApiKeyV5.KnownEnvironments.Local)]
            public void ValidEnvironment(string galleryEnvironment, char environment)
            {
                // Arrange & Act
                var result = ApiKeyV5.GetEnvironment(galleryEnvironment);

                // Assert
                Assert.Equal(environment, result);
            }

            [Fact]
            public void InvalidEnvironmentDefaultToLocal()
            {
                // Arrange & Act
                var result = ApiKeyV5.GetEnvironment("TestEnvironment");

                // Assert
                Assert.Equal(ApiKeyV5.KnownEnvironments.Local, result);
            }
        }

        public ApiKeyV5Facts()
        {
            AllocationTime = new DateTime(2024, 12, 5, 13, 30, 0, DateTimeKind.Utc);
            Environment = 'Z';
            UserKey = 1337;
            Type = ApiKeyV5.KnownApiKeyTypes.ShortLived;
            Expiration = TimeSpan.FromMinutes(15);
        }
    }
}
