// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace CatalogMetadataTests
{
    public class CommonLoggerFacts
    {
        private const string TestLogText = "dlvnsflvbjseirovj";

        [Fact]
        public void ThrowsForNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new CommonLogger(null));
        }

        public static IEnumerable<object[]> MethodToLevelsMap => new[]
        {
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogDebug(t)),              LogLevel.Debug },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogVerbose(t)),            LogLevel.Information },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogInformation(t)),        LogLevel.Information },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogInformationSummary(t)), LogLevel.Information },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogMinimal(t)),            LogLevel.Information },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogWarning(t)),            LogLevel.Warning },
            new object[] { (Action<CommonLogger, string>)((l, t) => l.LogError(t)),              LogLevel.Error },
        };

        [Theory]
        [MemberData(nameof(MethodToLevelsMap))]
        public void SpecificLogMethodConvertsLevelProperly(Action<CommonLogger, string> method, LogLevel expectedLogLevel)
        {
            ValidateLevel<object>(method, expectedLogLevel);
        }

        public static IEnumerable<object[]> ErrorLevelsMap => new[]
        {
            new object[] { NuGet.Common.LogLevel.Debug,       LogLevel.Debug },
            new object[] { NuGet.Common.LogLevel.Verbose,     LogLevel.Information },
            new object[] { NuGet.Common.LogLevel.Information, LogLevel.Information },
            new object[] { NuGet.Common.LogLevel.Minimal,     LogLevel.Information },
            new object[] { NuGet.Common.LogLevel.Warning,     LogLevel.Warning },
            new object[] { NuGet.Common.LogLevel.Error,       LogLevel.Error },
        };

        [Theory]
        [MemberData(nameof(ErrorLevelsMap))]
        public void GenericLogConvertsLogLevelCorrectly(NuGet.Common.LogLevel inputLogLevel, LogLevel expectedLogLevel)
        {
            ValidateLevel<string>((l, t) => l.Log(inputLogLevel, t), expectedLogLevel);
        }

        [Theory]
        [MemberData(nameof(ErrorLevelsMap))]
        public void GenericLogAsyncConvertsLogLevelCorrectly(NuGet.Common.LogLevel logLevel, LogLevel expectedLogLevel)
        {
            ValidateLevel<string>((l, t) => l.LogAsync(logLevel, t).Wait(), expectedLogLevel);
        }

        private static NuGet.Common.NuGetLogCode[] LogCodesToTest => (NuGet.Common.NuGetLogCode[])Enum.GetValues(typeof(NuGet.Common.NuGetLogCode));

        public static IEnumerable<object[]> LevelLogCodesToTest =
            from level in ErrorLevelsMap
            from code in LogCodesToTest
            select new object[] { level[0], level[1], code };

        [Theory]
        [MemberData(nameof(LevelLogCodesToTest))]
        public void GenericLogPassesEventCodeThroughAndConvertsLogLevelCorrectly(
            NuGet.Common.LogLevel logLevel,
            LogLevel expectedLogLevel,
            NuGet.Common.NuGetLogCode expectedCode)
        {
            ValidateLevelAndEventId<string>(
                (l, t) => l.Log(CreateLogMessage(logLevel, t, expectedCode)),
                expectedLogLevel,
                (int)expectedCode);
        }

        [Theory]
        [MemberData(nameof(LevelLogCodesToTest))]
        public void GenericLogAsyncPassesEventCodeThroughAndConvertsLogLevelCorrectly(
            NuGet.Common.LogLevel logLevel,
            LogLevel expectedLogLevel,
            NuGet.Common.NuGetLogCode expectedCode)
        {
            ValidateLevelAndEventId<string>(
                (l, t) => l.LogAsync(CreateLogMessage(logLevel, t, expectedCode)).Wait(),
                expectedLogLevel,
                (int)expectedCode);
        }

        private static NuGet.Common.ILogMessage CreateLogMessage(
            NuGet.Common.LogLevel logLevel,
            string message,
            NuGet.Common.NuGetLogCode code)
        {
            var messageMock = new Mock<NuGet.Common.ILogMessage>();
            messageMock
                .SetupProperty(m => m.Level, logLevel)
                .SetupProperty(m => m.Message, message)
                .SetupProperty(m => m.Code, code);

            return messageMock.Object;
        }

        private void ValidateLevel<T>(Action<CommonLogger, string> logMethod, LogLevel expectedLogLevel)
        {
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            loggerMock
                .Setup(l => l.Log(
                    It.Is<LogLevel>(level => level == expectedLogLevel),
                    It.IsAny<EventId>(),
                    It.Is<T>(s => s.ToString() == TestLogText),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<T, Exception, string>>()))
                .Verifiable();

            ValidateLogCall(logMethod, loggerMock);
        }

        private void ValidateLevelAndEventId<T>(Action<CommonLogger, string> logMethod, LogLevel expectedLogLevel, int expectedEventId)
        {
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            loggerMock
                .Setup(l => l.Log(
                    It.Is<LogLevel>(level => level == expectedLogLevel),
                    It.Is<EventId>(id => id.Id == expectedEventId),
                    It.Is<T>(s => s.ToString() == TestLogText),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<T, Exception, string>>()))
                .Verifiable();

            ValidateLogCall(logMethod, loggerMock);
        }

        private static void ValidateLogCall(Action<CommonLogger, string> logMethod, Mock<ILogger> loggerMock)
        {
            var logger = new CommonLogger(loggerMock.Object);
            logMethod.Invoke(logger, TestLogText);
            loggerMock.Verify();
        }
    }
}
