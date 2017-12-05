// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;
using Elmah;

namespace NuGetGallery
{
    internal static class QuietLog
    {
        public static ITelemetryClient Telemetry = TelemetryClientWrapper.Instance;

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
                var currentHttpContext = HttpContext.Current;
                if (currentHttpContext != null)
                {
                    ElmahException elmahException = new ElmahException(e, GetObfuscatedServerVariables(new HttpContextWrapper(currentHttpContext)));
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

        internal static bool IsPIIRoute(RouteData route, out string operation)
        {
            if(route == null)
            {
                operation = string.Empty;
                return false;
            }
            operation = $"{route.Values["controller"]}/{route.Values["action"]}";
            return ClientTelemetryPIIProcessor.PiiActions.Contains(operation);
        }

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
            Dictionary<string, string> obfuscatedServerVariables = new Dictionary<string, string>();
            var obfuscatedURL = ClientTelemetryPIIProcessor.DefaultObfuscatedUrl(currentContext.Request.Url);
            obfuscatedServerVariables.Add("HTTP_REFERER", obfuscatedURL);
            obfuscatedServerVariables.Add("PATH_INFO", operation);
            obfuscatedServerVariables.Add("PATH_TRANSLATED", operation);
            obfuscatedServerVariables.Add("SCRIPT_NAME", operation);
            obfuscatedServerVariables.Add("URL", obfuscatedURL);

            return obfuscatedServerVariables;
        }
    }
}