// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsSource
    {
        void TraceEvent(TraceEventType type, int id, string message,
            [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0);

        void PerfEvent(string name, TimeSpan time, IEnumerable<KeyValuePair<string, object>> payload);
    }
}
