// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class ApiKeyV3Facts
    {
        [Theory]
        [InlineData("abc")]
        [InlineData(null)]
        [InlineData("")]
        public void WhenCreateFromV1V2ApiKeyIsCalledWithInvalidPlaintextApiKeyItThrows(string input)
        {
            Assert.Throws<ArgumentException>(() => ApiKeyV3.CreateFromV1V2ApiKey(input));
        }

        [Theory]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "36aafd8744", "d7423badb9ed01493aeb75")]
        [InlineData("36AAFD87-44D7-423B-ADB9-ED01493AEB75", "36aafd8744", "d7423badb9ed01493aeb75")]
        [InlineData("c0488e19-e450-4512-8af2-06a29b7efac7", "c0488e19e4", "5045128af206a29b7efac7")]
        public void CreatesAValidApiKeyFromV1V2ApiKey(string plaintextApiKey, string expectedIdPart, string expectedPasswordPart)
        {
            // Act
            var apiKeyV3 = ApiKeyV3.CreateFromV1V2ApiKey(plaintextApiKey);

            // Assert
            Assert.Equal(plaintextApiKey.ToLower(), apiKeyV3.PlaintextApiKey);
            Assert.Equal(expectedIdPart, apiKeyV3.IdPart);
            Assert.Equal(expectedPasswordPart, apiKeyV3.PasswordPart);
            Assert.Equal(ApiKeyV3.IdAndPasswordHashedLength, apiKeyV3.HashedApiKey.Length);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData(null)]
        [InlineData("")]
        public void WhenTryParseIsCalledWithInvalidApiKeyReturnsFalse(string input)
        {
            Assert.False(ApiKeyV3.TryParse(input, out _));
        }

        [Theory]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "36aafd8744", "d7423badb9ed01493aeb75")]
        [InlineData("36AAFD87-44D7-423B-ADB9-ED01493AEB75", "36aafd8744", "d7423badb9ed01493aeb75")]
        [InlineData("c0488e19-e450-4512-8af2-06a29b7efac7", "c0488e19e4", "5045128af206a29b7efac7")]
        public void TryParseSucceedsForValidApiKey(string plaintextApiKey, string expectedIdPart, string expectedPasswordPart)
        {
            // Act
            bool result = ApiKeyV3.TryParse(plaintextApiKey, out ApiKeyV3 apiKeyV3);

            // Assert
            Assert.True(result);
            Assert.Equal(plaintextApiKey.ToLower(), apiKeyV3.PlaintextApiKey);
            Assert.Equal(expectedIdPart, apiKeyV3.IdPart);
            Assert.Equal(expectedPasswordPart, apiKeyV3.PasswordPart);
        }

        [Theory]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "36aafd87-44d7-423b-adb9-ed01493aeb75")]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "")]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", null)]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "36aafd8744AQAAAAEAACcQAAAAEKbL2BEqIg8KUMje6q69fW1f3jsVm+gOLf4nD5G392Til5KnGEyFHuwKEj3HEpZKug==")]
        public void WhenHashedValueDoesNotMatchVerifyReturnsFalse(string plaintextApiKey, string hash)
        {
            // Arrange
            Assert.True(ApiKeyV3.TryParse(plaintextApiKey, out var apiKeyV3));

            // Act & Assert
            Assert.False(apiKeyV3.Verify(hash));
        }

        [Theory]
        [InlineData("36aafd87-44d7-423b-adb9-ed01493aeb75", "36aafd8744AQAAAAEAACcQAAAAEKbL2BEqIg8KUMje6q69fW1f2jsVm+gOLf4nD5G392Til5KnGEyFHuwKEj3HEpZKug==")]
        [InlineData("36AAFD87-44D7-423B-ADB9-ED01493AEB75", "36aafd8744AQAAAAEAACcQAAAAEKbL2BEqIg8KUMje6q69fW1f2jsVm+gOLf4nD5G392Til5KnGEyFHuwKEj3HEpZKug==")]
        [InlineData("c0488e19-e450-4512-8af2-06a29b7efac7", "c0488e19e4AQAAAAEAACcQAAAAEMd7+9pjFEdRBVwSbzQ5tgBjBHL0Ac46yb8DwTvJ3R3vh5W3//9l3VWW/fLRBE+83g==")]
        public void WhenHashedValueMatchesVerifyReturnsTrue(string plaintextApiKey, string hash)
        {
            // Arrange
            Assert.True(ApiKeyV3.TryParse(plaintextApiKey, out var apiKeyV3));

            // Act & Assert
            Assert.True(apiKeyV3.Verify(hash));
        }

        [Theory]
        [InlineData("8ae347cd-aca1-42d9-9107-9053ffdbbf61")]
        [InlineData("fbc9897b-7b98-4669-96d6-f5349e54709c")]
        [InlineData("b11cd063-d9d3-436b-81b4-0bc2fbc97edd")]
        public void VerifySucceedsForApiKeysCreatedByCreateFromV1V2ApiKey(string v1v2ApiKey)
        {
            // Arrange
            var createdApiKeyV3 = ApiKeyV3.CreateFromV1V2ApiKey(v1v2ApiKey);
            ApiKeyV3.TryParse(v1v2ApiKey, out var parsedApiKeyV3);

            // Act & Assert
            Assert.True(parsedApiKeyV3.Verify(createdApiKeyV3.HashedApiKey));
        }
    }
}
