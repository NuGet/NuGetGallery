// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;
using Elmah;

namespace NuGetGallery
{
    public static class QuietLog
    {
        private static ITelemetryClient Telemetry;

        public static void UseTelemetryClient(ITelemetryClient telemetryClient)
        {
            Telemetry = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

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

        public static void LogHandledException(Exception e, ErrorLog errorLog)
        {
            var aggregateExceptionId = Guid.NewGuid().ToString();

            var aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                LogHandledExceptionCore(aggregateException, aggregateExceptionId, errorLog);

                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    LogHandledExceptionCore(innerException, aggregateExceptionId, errorLog);
                }
            }
            else
            {
                LogHandledExceptionCore(e, aggregateExceptionId, errorLog);

                if (e.InnerException != null)
                {
                    LogHandledExceptionCore(e.InnerException, aggregateExceptionId, errorLog);
                }
            }
        }

        private static void LogHandledExceptionCore(Exception e, string aggregateExceptionId)
        {
            try
            {
                var currentHttpContext = HttpContext.Current;
                if (currentHttpContext != null)
                {
                    var elmahException = new ElmahException(e, GetObfuscatedServerVariables(new HttpContextWrapper(currentHttpContext)));
                    ErrorSignal.FromCurrentContext().Raise(elmahException);
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

        private static void LogHandledExceptionCore(Exception e, string aggregateExceptionId, ErrorLog errorLog)
        {
            try
            {
                errorLog.Log(new Error(e));

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

        internal static bool IsPIIRoute(RouteData route, out string operation)
        {
            if(route == null)
            {
                operation = string.Empty;
                return false;
            }
            operation = $"{route.Values["controller"]}/{route.Values["action"]}";
            return Obfuscator.ObfuscatedActions.Contains(operation);
        }

        /// <summary>
        /// These values will be used to overwrite the serverVariables in the Elmah error before the exception is logged.
        /// These are the serverVariables that Gallery decides that will be obfuscated and the values to be used for obfuscation.
        /// </summary>
        /// <param name="currentContext">The current HttpContext.</param>
        /// <returns>A dictionary with the server variables that will be obfuscated.</returns>
        internal static Dictionary<string, string> GetObfuscatedServerVariables(HttpContextBase currentContext)
        {
            string operation = string.Empty;
            if(currentContext == null ||
               currentContext.Request == null ||
               currentContext.Request.RequestContext == null ||
               !IsPIIRoute(currentContext.Request.RequestContext.RouteData, out operation))
            {
                return null;
            }
            
            var obfuscatedServerVariables = new Dictionary<string, string>();
            var obfuscatedURL = Obfuscator.DefaultObfuscatedUrl(currentContext.Request.Url);
            obfuscatedServerVariables.Add("HTTP_REFERER", obfuscatedURL);
            obfuscatedServerVariables.Add("PATH_INFO", operation);
            obfuscatedServerVariables.Add("PATH_TRANSLATED", operation);
            obfuscatedServerVariables.Add("SCRIPT_NAME", operation);
            obfuscatedServerVariables.Add("URL", obfuscatedURL);

            return obfuscatedServerVariables;
        }
    }
}