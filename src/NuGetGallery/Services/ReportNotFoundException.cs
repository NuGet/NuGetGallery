// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace NuGetGallery
{
    /// <summary>
    /// Exception thrown when a report is not found.
    /// </summary>
    [Serializable]
    public class ReportNotFoundException : Exception
    {
        public ReportNotFoundException() { }
        public ReportNotFoundException(string message) : base(message) { }
        public ReportNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected ReportNotFoundException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }
}
