// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Cookies
{
    public class CookieComplianceServiceFacts
    {
        [Fact]
        public void InitializeCookieComplianceService_ThrowsIfCookieComplianceServiceNull()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => CookieComplianceService.Initialize(null, It.IsAny<ILogger>()));
            Assert.Equal("cookieComplianceService", exception.ParamName);
        }

        [Fact]
        public void InitializeCookieComplianceService_ThrowsIfLoggerNull()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => CookieComplianceService.Initialize(Mock.Of<ICookieComplianceService>(), null));
            Assert.Equal("logger", exception.ParamName);
        }
    }
}
