// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using Elmah;
using Microsoft.ApplicationInsights;

namespace NuGetGallery
{
    internal static class QuietLog
    {
        public static void LogHandledException(Exception e)
        {
            var aggregateExceptionId = Guid.NewGuid().ToString();

            var aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                LogHandledExceptionCore(aggregateException, aggregateExceptionId);

                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    LogHandledExceptionCore(innerException, aggregateExceptionId);
                }
            }
            else
            {
                LogHandledExceptionCore(e, aggregateExceptionId);

                if (e.InnerException != null)
                {
                    LogHandledExceptionCore(e.InnerException, aggregateExceptionId);
                }
            }
        }

        private static void LogHandledExceptionCore(Exception e, string aggregateExceptionId)
        {
            try
            {
                if (HttpContext.Current != null)
                {
                    ErrorSignal.FromCurrentContext().Raise(e);
                }
                else
                {
                    ErrorLog.GetDefault(null).Log(new Error(e));
                }

                // send exception to AppInsights
                Telemetry.TrackException(e, new Dictionary<string, string>
                {
                    { "aggregateExceptionId", aggregateExceptionId }
                });
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}