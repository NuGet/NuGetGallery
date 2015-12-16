// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Indexing
{
    public static class ServiceHelpers
    {
        public static void TraceException(Exception e)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    TraceException(ex);
                }
            }
            else
            {
                Trace.TraceError("{0} {1}", e.GetType().Name, e.Message);
                Trace.TraceError("{0}", e.StackTrace);

                if (e.InnerException != null)
                {
                    TraceException(e.InnerException);
                }
            }
        }
    }
}
