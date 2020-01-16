// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Moq;
using Xunit;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsServiceFacts
    {
        public class TheGetSourceMethod
        {
            [Fact]
            public void RequiresNonNullOrEmptyName()
            {
                // Arrange
                var telemetryClient = new Mock<ITelemetryClient>().Object;
                var diagnosticsService = new DiagnosticsService(telemetryClient);

                // Act and Assert
                ContractAssert.ThrowsArgNullOrEmpty(s => diagnosticsService.GetSource(s), "name");
            }
        }
    }
}
