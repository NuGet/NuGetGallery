// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    internal class XunitLoggerProvider
        : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public XunitLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_testOutputHelper);
        }

        private class XunitLogger
            : ILogger
        {
            private readonly ITestOutputHelper _testOutputHelper;

            public XunitLogger(ITestOutputHelper testOutputHelper)
            {
                _testOutputHelper = testOutputHelper;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                // This code uses FormattedLogValues class that became internal between v2 and v8 of
                // Microsoft.Extensions.Logging.Abstractions package preventing us from updating.
                // To unblock, we'll instantiate it using reflection, yes it is ugly. It is also test code.
                // We're also working on getting rid of the project that is being tested using it. So, it should be gone soon.
                Assembly abstractionsAssembly = Assembly.GetAssembly(typeof(NullLogger));
                Type formattedLogValuesType = abstractionsAssembly.GetType("Microsoft.Extensions.Logging.FormattedLogValues");
                var message = Activator.CreateInstance(formattedLogValuesType, formatter.Invoke(state, exception));
                _testOutputHelper.WriteLine($"{logLevel} - {eventId} - {message}");
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}
