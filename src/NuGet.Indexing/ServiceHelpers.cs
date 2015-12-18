// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class ServiceHelpers
    {
        public static void TraceException(Exception e, FrameworkLogger logger)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    TraceException(ex, logger);
                }
            }
            else
            {
                logger.LogError($"{e.GetType().Name} {e.Message}", e);

                if (e.InnerException != null)
                {
                    TraceException(e.InnerException, logger);
                }
            }
        }
    }
}
