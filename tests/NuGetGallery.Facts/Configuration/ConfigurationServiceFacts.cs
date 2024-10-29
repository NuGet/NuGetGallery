// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Framework;
using System;
using Xunit;

namespace NuGetGallery.Configuration
{
    public class ConfigurationServiceFacts
    {
        public class TheExternalBrandingMessage : TestContainer
        {
            [Theory]
            [InlineData("&#169; MyCompany {0}")]
            [InlineData("&#169; MyCompany 2020")]
            public void TheMessageIsFormattedCorrectly(string message)
            {
                // Arrange
                var service = GetConfigurationService();
                var currentYear = DateTime.UtcNow.Year;
                var expectedBrandingMessage = string.Format(message, currentYear);

                // Act
                service.Current.ExternalBrandingMessage = message;

                // Assert
                Assert.Equal(expectedBrandingMessage, service.Current.ExternalBrandingMessage);
            }
        }
    }
}