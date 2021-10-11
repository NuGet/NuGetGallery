// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Infrastructure.Authentication
{
    public class ApiKeyV4Facts
    {
        [Fact]
        public void CreatesAValidApiKey()
        {
            // Act
            var apiKey = ApiKeyV4.Create();

            // Assert
            Assert.NotNull(apiKey);
            Assert.NotNull(apiKey.HashedApiKey);
            Assert.Equal(ApiKeyV4.IdAndPasswordHashedLength, apiKey.HashedApiKey.Length);

            Assert.NotNull(apiKey.PlaintextApiKey);
            Assert.Equal(ApiKeyV4.IdAndPasswordLength, apiKey.PlaintextApiKey.Length);
            Assert.Equal(apiKey.PlaintextApiKey.ToLower(), apiKey.PlaintextApiKey);

            Assert.NotNull(apiKey.IdPart);
            Assert.Equal(ApiKeyV4.IdPartBase32Length, apiKey.IdPart.Length);

            Assert.NotNull(apiKey.PasswordPart);
            Assert.Equal(ApiKeyV4.IdAndPasswordLength - ApiKeyV4.IdPartBase32Length, apiKey.PasswordPart.Length);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("abc")]
        [InlineData("SEMTXET5UU6UZDD4AMK57TR46I==")]
        [InlineData("0000thisis46charactersbutnotvalidbase32encoded")]
        public void TryParseFailsForIllegalApiKeys(string inputApiKey)
        {
            // Act 
            bool result = ApiKeyV4.TryParse(inputApiKey, out var apiKey);

            // Assert
            Assert.False(result);
        }


        [Theory]
        [InlineData("oy2iuvoucviouojnrbzaqxjewdop3yseppnqlewvavhupm", "oy2iuvoucviouojnrbza", "qxjewdop3yseppnqlewvavhupm")]
        [InlineData("OY2IUVOUCVIOUOJNRBZAQXJEWDOP3YSEPPNQLEWVAVHUPM", "oy2iuvoucviouojnrbza", "qxjewdop3yseppnqlewvavhupm")]
        public void TryParseSucceedsForValidApiKeys(string inputApiKey, string idPart, string passwordPart)
        {
            // Act 
            bool result = ApiKeyV4.TryParse(inputApiKey, out var apiKey);

            // Assert
            Assert.True(result);
            Assert.Equal(inputApiKey, apiKey.PlaintextApiKey);
            Assert.Equal(idPart, apiKey.IdPart);
            Assert.Equal(passwordPart, apiKey.PasswordPart);
            Assert.Null(apiKey.HashedApiKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("oy2iuvoucviouojnrbzaqxjewdop3yseppnqlewvavhupm")]
        public void VerifyFailsForIllegalInput(string hashedApiKey)
        {
            // Arrange
            var apiKey = ApiKeyV4.Create();

            // Act & Assert
            Assert.False(apiKey.Verify(hashedApiKey));
        }

        [Fact]
        public void VerifyFailsOnIncorrectHashedKey()
        {
            // Arrange
            var apiKey = ApiKeyV4.Create();
            var hashedApiKey = "oy2iuvoucviouojnrbzaAEAAAAABAAACOEAAAAABAMHUIIUXSFAIOTEAFWF3K2E7L6YRUDOZDPHKSRX3YEOFUWPCID325EZH5JXBBGBMYBECT4KCGMWOQQ======";

            // Act & Assert
            Assert.False(apiKey.Verify(hashedApiKey));
        }


        [Theory]
        [InlineData("oy2iuvoucviouojnrbzaqxjewdop3yseppnqlewvavhupm", "oy2iuvoucviouojnrbzaAEAAAAABAAACOEAAAAABAMHUIIUXSFAIOTEAFWF3K2E7L6YRUDOZDPHKSRX3YEOFUWPCID325EZH5JXBBGBMYBECT4KCGMWOQQ======")]
        [InlineData("OY2IUVOUCVIOUOJNRBZAQXJEWDOP3YSEPPNQLEWVAVHUPM", "oy2iuvoucviouojnrbzaAEAAAAABAAACOEAAAAABAMHUIIUXSFAIOTEAFWF3K2E7L6YRUDOZDPHKSRX3YEOFUWPCID325EZH5JXBBGBMYBECT4KCGMWOQQ======")]
        public void VerifySucceedsOnValidApiKeys(string inputApiKey, string hashedApiKey)
        {
            // Arrange 
            Assert.True(ApiKeyV4.TryParse(inputApiKey, out var apiKey));

            // Act & Assert
            Assert.True(apiKey.Verify(hashedApiKey));
        }

        [Fact]
        public void VerifySucceedsOnKeysCreatedByCreate()
        {
            // Arrange
            var apiKey = ApiKeyV4.Create();
            ApiKeyV4.TryParse(apiKey.PlaintextApiKey, out var parsedApiKey);

            // Act & Assert
            Assert.True(parsedApiKey.Verify(apiKey.HashedApiKey));
        }
    }
}

