// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NgTests.Infrastructure
{
    internal class TestLoggerFactory
        : ILoggerFactory
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestLoggerFactory(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_testOutputHelper);
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public LogLevel MinimumLevel { get; set; }
    }
}