// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch.Support
{
    public class RecordingLogger<T> : ILogger<T>
    {
        private readonly ILogger<T> _inner;
        private readonly ConcurrentStack<string> _messages = new ConcurrentStack<string>();

        public RecordingLogger(ILogger<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IReadOnlyList<string> Messages => _messages.ToList();

        public virtual IDisposable BeginScope<TState>(TState state)
        {
            return _inner.BeginScope(state);
        }

        public virtual bool IsEnabled(LogLevel logLevel)
        {
            return _inner.IsEnabled(logLevel);
        }

        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _messages.Push(formatter(state, exception));
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
