// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class TelemetryServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsIfDiagnosticsServiceIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => new TelemetryService(null));
            }
        }

        public class TheTraceExceptionMethod : BaseFacts
        {
            [Fact]
            public void ThrowsIfExceptionIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => service.TraceException(null));
            }

            [Fact]
            public void CallsTraceEvent()
            {
                // Arrange
                var service = CreateService();

                // Act
                service.TraceException(new InvalidOperationException("Example"));

                // Assert
                service.TraceSource.Verify(t => t.TraceEvent(
                        TraceEventType.Warning,
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()),
                    Times.Once);
                Assert.True(service.LastTraceMessage.Contains("InvalidOperationException"));
            }
        }

        public class BaseFacts
        {
            public class TelemetryServiceWrapper : TelemetryService
            {
                public TelemetryServiceWrapper(IDiagnosticsService diagnosticsService)
                    : base(diagnosticsService)
                {
                }

                public Mock<IDiagnosticsSource> TraceSource { get; set; }

                public string LastTraceMessage { get; set; }
            }

            public TelemetryServiceWrapper CreateService()
            {
                var traceSource = new Mock<IDiagnosticsSource>();
                var traceService = new Mock<IDiagnosticsService>();

                traceService.Setup(s => s.GetSource(It.IsAny<string>()))
                    .Returns(traceSource.Object);

                var telemetryService = new TelemetryServiceWrapper(traceService.Object);

                telemetryService.TraceSource = traceSource;

                traceSource.Setup(t => t.TraceEvent(
                        It.IsAny<TraceEventType>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()))
                    .Callback<TraceEventType, int, string, string, string, int>(
                        (type, id, message, member, file, line) => telemetryService.LastTraceMessage = message)
                    .Verifiable();

                return telemetryService;
            }
        }
    }
}
