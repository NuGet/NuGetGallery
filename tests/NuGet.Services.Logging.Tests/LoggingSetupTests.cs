// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class LoggingSetupTests
    {
        [Fact]
        public void CreateLoggerFactory_AddsSerilogTraceListener()
        {
            using (new TraceListenerCollectionResetter())
            {
                Assert.Equal(0, GetSerilogTraceListenerCount());

                LoggingSetup.CreateLoggerFactory();

                Assert.Equal(1, GetSerilogTraceListenerCount());
            }
        }

        [Fact]
        public void CreateLoggerFactory_AddsSerilogTraceListenerOnlyOnce()
        {
            using (new TraceListenerCollectionResetter())
            {
                Assert.Equal(0, GetSerilogTraceListenerCount());

                LoggingSetup.CreateLoggerFactory();
                LoggingSetup.CreateLoggerFactory();

                Assert.Equal(1, GetSerilogTraceListenerCount());
            }
        }

        private static int GetSerilogTraceListenerCount()
        {
            return Trace.Listeners.OfType<SerilogTraceListener.SerilogTraceListener>().Count();
        }

        private sealed class TraceListenerCollectionResetter : IDisposable
        {
            private readonly TraceListener[] _listeners;

            internal TraceListenerCollectionResetter()
            {
                _listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
            }

            public void Dispose()
            {
                Trace.Listeners.Clear();
                Trace.Listeners.AddRange(_listeners);
            }
        }
    }
}