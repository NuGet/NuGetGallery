// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class CoreMessageServiceConfigurationFacts
    {
        [Fact]
        public void ThrowsWhenEmailConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new CoreMessageServiceConfiguration(null));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenEmailConfigurationIsNull()
        {
            var ex = Assert.Throws<ArgumentException>(() => new CoreMessageServiceConfiguration(EmailConfigurationAccessorMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
            Assert.Contains("Value", ex.Message);
            Assert.DoesNotContain("Value.", ex.Message);
        }

        [Theory]
        [InlineData(null, "123", "Value.GalleryOwner")]
        [InlineData("", "123", "Value.GalleryOwner")]
        [InlineData(" ", "123", "Value.GalleryOwner")]
        [InlineData("123", null, "Value.GalleryNoReplyAddress")]
        [InlineData("123", "", "Value.GalleryNoReplyAddress")]
        [InlineData("123", " ", "Value.GalleryNoReplyAddress")]
        public void ThrowsWhenConfigurationPropertiesAreInvalid(string galleryOwner, string galleryNoReplyAddress, string expectedProperty)
        {
            EmailConfigurationAccessorMock
                .SetupGet(eca => eca.Value)
                .Returns(new EmailConfiguration { GalleryOwner = galleryOwner, GalleryNoReplyAddress = galleryNoReplyAddress });

            var ex = Assert.Throws<ArgumentException>(() => new CoreMessageServiceConfiguration(EmailConfigurationAccessorMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
            Assert.Contains(expectedProperty, ex.Message);
        }

        public Mock<IOptionsSnapshot<EmailConfiguration>> EmailConfigurationAccessorMock { get; } = new Mock<IOptionsSnapshot<EmailConfiguration>>();
    }
}
