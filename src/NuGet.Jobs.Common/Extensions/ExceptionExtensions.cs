// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Jobs
{
    public static class ExceptionExtensions
    {
        private static void TraceAggregateException(this AggregateException exception)
        {
            var innerEx = exception.InnerExceptions.Count > 0 ? exception.InnerExceptions[0] : null;
            if (innerEx != null)
            {
                Trace.TraceError("[FAILED]: " + innerEx);
            }
            else
            {
                Trace.TraceError("[FAILED]: " + exception);
            }
        }

        public static void TraceException(this Exception exception)
        {
            var aggregateException = exception as AggregateException;
            if (aggregateException != null)
            {
                aggregateException.TraceAggregateException();
            }
            else
            {
                Trace.TraceError("[FAILED]: " + exception);
            }
        }
    }
}