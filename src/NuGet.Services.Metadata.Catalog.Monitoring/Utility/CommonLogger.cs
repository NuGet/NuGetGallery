// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// <see cref="Common.ILogger"/> wrapper for <see cref="ILogger"/>.
    /// </summary>
    public class CommonLogger : Common.ILogger
    {
        // This event ID is believed to be unused anywhere else but is otherwise arbitrary.
        private const int DefaultLogEventId = 23847;
        private static EventId DefaultClientLogEvent = new EventId(DefaultLogEventId);

        public CommonLogger(Microsoft.Extensions.Logging.ILogger logger)
        {
            InternalLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Microsoft.Extensions.Logging.ILogger InternalLogger { get; private set; }

        public void LogDebug(string data)
        {
            InternalLogger.LogDebug(data);
        }

        public void LogVerbose(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogInformation(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogWarning(string data)
        {
            InternalLogger.LogWarning(data);
        }

        public void LogError(string data)
        {
            InternalLogger.LogError(data);
        }

        public void LogInformationSummary(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogErrorSummary(string data)
        {
            InternalLogger.LogError(data);
        }

        public void Log(Common.LogLevel level, string data)
        {
            InternalLogger.Log(GetLogLevel(level), DefaultClientLogEvent, data, null, (str, ex) => str);
        }

        public Task LogAsync(Common.LogLevel level, string data)
        {
            InternalLogger.Log(GetLogLevel(level), DefaultClientLogEvent, data, null, (str, ex) => str);
            return Task.FromResult<object>(null);
        }

        public void Log(ILogMessage message)
        {
            InternalLogger.Log(GetLogLevel(message.Level), new EventId((int)message.Code), message.Message, null, (str, ex) => str);
        }

        public Task LogAsync(ILogMessage message)
        {
            InternalLogger.Log(GetLogLevel(message.Level), new EventId((int)message.Code), message.Message, null, (str, ex) => str);
            return Task.FromResult<object>(null);
        }

        private static Microsoft.Extensions.Logging.LogLevel GetLogLevel(Common.LogLevel logLevel)
        {
            switch (logLevel)
            {
            case Common.LogLevel.Debug:
                return Microsoft.Extensions.Logging.LogLevel.Debug;

            case Common.LogLevel.Verbose:
                return Microsoft.Extensions.Logging.LogLevel.Information;

            case Common.LogLevel.Information:
                return Microsoft.Extensions.Logging.LogLevel.Information;

            case Common.LogLevel.Minimal:
                return Microsoft.Extensions.Logging.LogLevel.Information;

            case Common.LogLevel.Warning:
                return Microsoft.Extensions.Logging.LogLevel.Warning;

            case Common.LogLevel.Error:
                return Microsoft.Extensions.Logging.LogLevel.Error;
            }

            return Microsoft.Extensions.Logging.LogLevel.None;
        }
    }
}
