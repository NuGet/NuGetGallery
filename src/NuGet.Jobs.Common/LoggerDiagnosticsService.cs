// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;
using NuGetGallery.Diagnostics;

namespace NuGet.Jobs
{
    public class LoggerDiagnosticsService : IDiagnosticsService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ITelemetryClient _telemetryClient;

        public LoggerDiagnosticsService(ILoggerFactory loggerFactory, ITelemetryClient telemetryClient)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public IDiagnosticsSource GetSource(string name)
        {
            return new LoggerDiagnosticsSource(
                _telemetryClient,
                _loggerFactory.CreateLogger(name));
        }
    }
}
