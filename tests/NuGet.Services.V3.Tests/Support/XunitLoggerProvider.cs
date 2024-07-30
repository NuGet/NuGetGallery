// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit.Abstractions;

namespace Microsoft.Extensions.Logging
{
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        private readonly LogLevel _minLevel;

        public XunitLoggerProvider(ITestOutputHelper output)
            : this(output, LogLevel.Trace)
        {
        }

        public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel)
        {
            _output = output;
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName, _minLevel);
        }

        public void Dispose()
        {
        }
    }
}