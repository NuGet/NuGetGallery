// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLogEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public Exception Exception { get; set; }
        public string Logger { get; set; }
        public JobLogEvent FullEvent { get; set; }
    }

    public class JobLogEvent
    {
        public int SequenceID { get; set; }
        public DateTime TimeStamp { get; set; }
        public LogLevel Level { get; set; }
        public string LoggerName { get; set; }
        public string LoggerShortName { get; set; }
        public string Message { get; set; }
        
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification="This is a JSON serialized object")]
        public string[] Parameters { get; set; }
        public string FormattedMessage { get; set; }
    }
}
