// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace NuGetGallery
{
    /// <summary>
    /// Exception thrown when the stats report is not found.
    /// </summary>
    [Serializable]
    public class StatisticsReportNotFoundException : Exception
    {
        public StatisticsReportNotFoundException() { }
        public StatisticsReportNotFoundException(string message) : base(message) { }
        public StatisticsReportNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected StatisticsReportNotFoundException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }
}