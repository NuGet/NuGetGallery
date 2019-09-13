// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Diagnostics
{
    public class NullDiagnosticsSource : IDiagnosticsSource
    {
        public static readonly NullDiagnosticsSource Instance = new NullDiagnosticsSource();

        private NullDiagnosticsSource() { }

        public void ExceptionEvent(Exception exception)
        {
            // No-op!
        }

        public void TraceEvent(LogLevel logLevel, EventId eventId, string message, string member = null, string file = null, int line = 0)
        {
            // No-op!
        }

        public void PerfEvent(string name, TimeSpan time, IEnumerable<KeyValuePair<string, object>> payload)
        {
            // No-op!
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // No-op!
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}