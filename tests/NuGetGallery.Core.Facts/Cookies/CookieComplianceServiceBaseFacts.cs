// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery.Cookies
{
    public class CookieComplianceServiceBaseFacts
    {
        [Fact]
        public void SiteName_ThrowsInvalidOperationExceptionIfNotInitialized()
        {
            var service = new Mock<CookieComplianceServiceBase>().Object;

            Assert.Throws<InvalidOperationException>(() =>
            {
                var result = service.SiteName;
            });
        }

        [Fact]
        public void Diagnostics_ThrowsInvalidOperationExceptionIfNotInitialized()
        {
            var service = new Mock<CookieComplianceServiceBase>().Object;

            Assert.Throws<InvalidOperationException>(() =>
            {
                var result = service.Diagnostics;
            });
        }

        [Fact]
        public async Task SiteName_ReturnsValueIfInitialized()
        {
            // Arrange
            var service = new Mock<CookieComplianceServiceBase>() { CallBase = true }.Object;
            var diagnostics = new Mock<IDiagnosticsService>().Object;
            await service.InitializeAsync("nuget.org", diagnostics);

            // Act & Assert
            Assert.Equal("nuget.org", service.SiteName);
        }

        [Fact]
        public async Task Diagnostics_ReturnsValueIfInitialized()
        {
            // Arrange
            var service = new Mock<CookieComplianceServiceBase>() { CallBase = true}.Object;

            var diagnostics = new Mock<IDiagnosticsService>();
            diagnostics.Setup(d => d.GetSource(It.IsAny<string>()))
                .Returns(new Mock<IDiagnosticsSource>().Object);

            await service.InitializeAsync("nuget.org", diagnostics.Object);

            // Act & Assert
            Assert.NotNull(service.Diagnostics);
        }

        [Theory]
        [InlineData("en-GB")]
        [InlineData("fr-FR")]
        public void Locale_ReturnsCurrentCulture(string currentCulture)
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                // Arrange
                var service = new Mock<CookieComplianceServiceBase>().Object;
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(currentCulture);

                // Act & Assert
                Assert.Equal(currentCulture, service.Locale);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }
    }
}
