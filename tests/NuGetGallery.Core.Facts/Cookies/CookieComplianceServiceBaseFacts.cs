// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery.Cookies
{
    public class CookieComplianceServiceBaseFacts
    {
        /*
        [Fact]
        public void Domain_ThrowsInvalidOperationExceptionIfNotInitialized()
        {
            var service = new Mock<InternalCookieComplianceService>().Object;

            Assert.Throws<InvalidOperationException>(() =>
            {
                var result = service.Domain;
            });
        }

        [Fact]
        public void Diagnostics_ThrowsInvalidOperationExceptionIfNotInitialized()
        {
            var service = new Mock<InternalCookieComplianceService>().Object;

            Assert.Throws<InvalidOperationException>(() =>
            {
                var result = service.Diagnostics;
            });
        }

        [Fact]
        public async Task Domain_ReturnsValueIfInitialized()
        {
            // Arrange
            var service = new Mock<InternalCookieComplianceService>() { CallBase = true }.Object;
            var diagnostics = new Mock<IDiagnosticsService>().Object;
            await service.InitializeAsync("nuget.org", diagnostics, CancellationToken.None);

            // Act & Assert
            Assert.Equal("nuget.org", service.Domain);
        }

        [Fact]
        public async Task Diagnostics_ReturnsValueIfInitialized()
        {
            // Arrange
            var service = new Mock<InternalCookieComplianceService>() { CallBase = true}.Object;

            var diagnostics = new Mock<IDiagnosticsService>();
            diagnostics.Setup(d => d.GetSource(It.IsAny<string>()))
                .Returns(new Mock<IDiagnosticsSource>().Object);

            await service.InitializeAsync("nuget.org", diagnostics.Object, CancellationToken.None);

            // Act & Assert
            Assert.NotNull(service.Diagnostics);
        }

        // Added to avoid InternalsVisibleTo in Gallery.Core which can be delay signed.
        public class InternalCookieComplianceService : NullCookieComplianceService
        {
            public new IDiagnosticsSource Diagnostics => base.Diagnostics;
            public new string Domain => base.Domain;
        }*/
    }
}
