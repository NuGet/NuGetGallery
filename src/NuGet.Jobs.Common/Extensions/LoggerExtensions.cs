// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;

namespace NuGet.Jobs.Extensions
{
    public static class LoggerExtensions
    {
        /// <summary>
        /// Calls <see cref="ILogger.BeginScope{TState}(TState)"/> and logs a message when entering and leaving the scope.
        /// </summary>
        public static IDisposable Scope(
            this ILogger logger, 
            string message, 
            params object[] args)
        {
            return new LoggerScopeHelper(logger, message, args);
        }

        private class LoggerScopeHelper : IDisposable
        {
            private readonly ILogger _logger;
            private readonly IDisposable _scope;

            private readonly string _message;
            private readonly object[] _args;

            private bool _isDisposed = false;

            public LoggerScopeHelper(
                ILogger logger, string message, object[] args)
            {
                _logger = logger;
                _message = message;
                _args = args;

                _scope = logger.BeginScope(_message, _args);
                _logger.LogInformation("Entering scope: " + _message, _args);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _logger.LogInformation("Leaving scope: " + _message, _args);
                    _scope?.Dispose(); // ILogger can return a null scope (most notably during testing with a Mock<ILogger>)
                    _isDisposed = true;
                }
            }
        }
    }
}
