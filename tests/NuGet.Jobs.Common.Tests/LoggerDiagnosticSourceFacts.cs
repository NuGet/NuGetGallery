// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Logging;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Common.Tests
{
    public class LoggerDiagnosticSourceFacts
    {
        public class TraceEvent : BaseFacts
        {
            private readonly TraceEventType _traceEventType;
            private readonly int _id;
            private readonly string _message;
            private readonly string _member;
            private readonly string _file;
            private readonly int _line;

            public TraceEvent(ITestOutputHelper output) : base(output)
            {
                _traceEventType = TraceEventType.Information;
                _id = 23;
                _message = "So interesting!";
                _member = "MyClass";
                _file = "MyClass.cs";
                _line = 42;
            }

            [Fact]
            public void UsesCallerInfo()
            {
                // Arrange
                LoggerDiagnosticsSource.TraceState state = null;
                _logger
                    .Setup(x => x.Log(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.IsAny<LoggerDiagnosticsSource.TraceState>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()))
                    .Callback<LogLevel, EventId, LoggerDiagnosticsSource.TraceState, Exception, Func<LoggerDiagnosticsSource.TraceState, Exception, string>>(
                        (_, __, s, ___, ____) => state = s);

                // Act
                _target.TraceEvent(
                    _traceEventType,
                    _id,
                    _message);

                // Assert
                Assert.NotNull(state);
                Assert.NotNull(state.Member);
                Assert.NotEqual(string.Empty, state.Member);
                Assert.NotNull(state.File);
                Assert.NotEqual(string.Empty, state.File);
                Assert.NotEqual(default(int), state.Line);
            }

            [Fact]
            public void LogsCorrectState()
            {
                // Arrange
                LoggerDiagnosticsSource.TraceState state = null;
                _logger
                    .Setup(x => x.Log(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.IsAny<LoggerDiagnosticsSource.TraceState>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()))
                    .Callback<LogLevel, EventId, LoggerDiagnosticsSource.TraceState, Exception, Func<LoggerDiagnosticsSource.TraceState, Exception, string>>(
                        (_, __, s, ___, ____) => state = s);

                // Act
                _target.TraceEvent(
                    _traceEventType,
                    _id,
                    _message,
                    _member,
                    _file,
                    _line);

                // Assert
                Assert.NotNull(state);
                Assert.Equal(_traceEventType, state.TraceEventType);
                Assert.Equal(_id, state.Id);
                Assert.Equal(_message, state.Message);
                Assert.Equal(_member, state.Member);
                Assert.Equal(_file, state.File);
                Assert.Equal(_line, state.Line);

                var pairs = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object>>>(state);
                Assert.Equal(new[] { "File", "Id", "Line", "Member", "Message", "TraceEventType" }, pairs.Select(x => x.Key).OrderBy(x => x));
                var pairDictionary = pairs.ToDictionary(x => x.Key, x => x.Value);
                Assert.Equal(_traceEventType, pairDictionary["TraceEventType"]);
                Assert.Equal(_id, pairDictionary["Id"]);
                Assert.Equal(_message, pairDictionary["Message"]);
                Assert.Equal(_member, pairDictionary["Member"]);
                Assert.Equal(_file, pairDictionary["File"]);
                Assert.Equal(_line, pairDictionary["Line"]);
            }

            [Theory]
            [InlineData(TraceEventType.Critical, LogLevel.Critical)]
            [InlineData(TraceEventType.Error, LogLevel.Error)]
            [InlineData(TraceEventType.Information, LogLevel.Information)]
            [InlineData(TraceEventType.Resume, LogLevel.Trace)]
            [InlineData(TraceEventType.Start, LogLevel.Trace)]
            [InlineData(TraceEventType.Stop, LogLevel.Trace)]
            [InlineData(TraceEventType.Suspend, LogLevel.Trace)]
            [InlineData(TraceEventType.Transfer, LogLevel.Trace)]
            [InlineData(TraceEventType.Verbose, LogLevel.Trace)]
            [InlineData(TraceEventType.Warning, LogLevel.Warning)]
            [InlineData((TraceEventType)(-1), LogLevel.Trace)]
            public void MapsToLogLevel(TraceEventType traceEventType, LogLevel logLevel)
            {
                // Arrange & Act
                _target.TraceEvent(
                    traceEventType,
                    _id,
                    _message,
                    _member,
                    _file,
                    _line);

                // Assert
                var actualMessage = Assert.Single(_logger.Object.Messages);
                Assert.Equal(_message, actualMessage);

                _logger.Verify(
                    x => x.Log(
                        logLevel,
                        _id,
                        It.IsAny<LoggerDiagnosticsSource.TraceState>(),
                        null,
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()),
                    Times.Once);
                _logger.Verify(
                    x => x.Log(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.IsAny<LoggerDiagnosticsSource.TraceState>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(LogLevel.Critical, TraceEventType.Critical)]
            [InlineData(LogLevel.Error, TraceEventType.Error)]
            [InlineData(LogLevel.Warning, TraceEventType.Warning)]
            [InlineData(LogLevel.Trace, TraceEventType.Information)]
            [InlineData(LogLevel.Debug, TraceEventType.Verbose)]
            [InlineData(LogLevel.Information, TraceEventType.Information)]
            [InlineData(LogLevel.None, TraceEventType.Information)]
            [InlineData((LogLevel)(-1), TraceEventType.Information)]
            public void MapsToTraceEventType(LogLevel logLevel, TraceEventType traceEventType)
            {
                // Arrange
                LoggerDiagnosticsSource.TraceState state = null;
                _logger
                    .Setup(x => x.Log(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.IsAny<LoggerDiagnosticsSource.TraceState>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()))
                    .Callback<LogLevel, EventId, LoggerDiagnosticsSource.TraceState, Exception, Func<LoggerDiagnosticsSource.TraceState, Exception, string>>(
                        (_, __, s, ___, ____) => state = s);

                // Act
                _target.TraceEvent(
                    logLevel,
                    new EventId(_id),
                    _message,
                    _member,
                    _file,
                    _line);

                // Assert
                Assert.Equal(traceEventType, state.TraceEventType);
                Assert.Equal(_id, state.Id);

                _logger.Verify(
                    x => x.Log(
                        logLevel,
                        _id,
                        state,
                        null,
                        It.IsAny<Func<LoggerDiagnosticsSource.TraceState, Exception, string>>()),
                    Times.Once);
            }
        }

        public class ExceptionEvent : BaseFacts
        {
            public ExceptionEvent(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void TracksException()
            {
                // Arrange
                var exception = new InvalidOperationException("Something bad.");

                // Act
                _target.ExceptionEvent(exception);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackException(exception, null, null),
                    Times.Once);
            }
        }

        public class PerfEvent : BaseFacts
        {
            private readonly string _name;
            private readonly TimeSpan _time;
            private Dictionary<string, object> _payload;

            public PerfEvent(ITestOutputHelper output) : base(output)
            {
                _name = "SomeTimedEvent";
                _time = TimeSpan.FromTicks(23420009);
                _payload = new Dictionary<string, object>
                {
                    { "Age", 10 },
                    { "Cost", null },
                    { "Created", new DateTimeOffset(2017, 1, 3, 8, 30, 0, TimeSpan.FromHours(-8)) },
                };
            }

            [Fact]
            public void AllowsNullPayload()
            {
                // Arrange & Act
                _target.PerfEvent(
                    _name,
                    _time,
                    payload: null);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric(_name, _time.TotalMilliseconds, null),
                    Times.Once);
            }

            [Fact]
            public void TracksMetric()
            {
                // Arrange
                IDictionary<string, string> properties = null;
                _telemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => properties = p);

                // Act
                _target.PerfEvent(
                    _name,
                    _time,
                    _payload);

                // Assert
                _telemetryClient.Verify(
                    x => x.TrackMetric(_name, _time.TotalMilliseconds, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                Assert.Equal(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("Age", "10"),
                    new KeyValuePair<string, string>("Cost", null),
                    new KeyValuePair<string, string>("Created", _payload["Created"].ToString()),
                }, properties.OrderBy(x => x.Key).ToList());
            }
        }

        public abstract class BaseFacts
        {
            protected readonly ITestOutputHelper _output;
            protected readonly Mock<ITelemetryClient> _telemetryClient;
            protected readonly Mock<RecordingLogger<LoggerDiagnosticSourceFacts>> _logger;
            protected readonly LoggerDiagnosticsSource _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _output = output;
                _telemetryClient = new Mock<ITelemetryClient>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                var innerLogger = loggerFactory.CreateLogger<LoggerDiagnosticSourceFacts>();
                _logger = new Mock<RecordingLogger<LoggerDiagnosticSourceFacts>>(innerLogger)
                {
                    CallBase = true,
                };

                _target = new LoggerDiagnosticsSource(
                    _telemetryClient.Object,
                    _logger.Object);
            }
        }
    }
}
