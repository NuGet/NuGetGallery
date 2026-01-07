// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsSource : ILogger
    {
        void ExceptionEvent(Exception exception);

        void TraceEvent(LogLevel logLevel, EventId eventId, string message,
            [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0);
    }
}