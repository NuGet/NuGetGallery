// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using Elmah.Contrib.Mvc;
using Microsoft.ApplicationInsights;

namespace NuGetGallery.Diagnostics
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class SendErrorsToTelemetryAttribute
        : ElmahHandleErrorAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            base.OnException(context);

            if (context != null)
            {
                try
                {
                    if (context.HttpContext != null && context.Exception != null)
                    {
                        // If customError is Off, then AppInsights HTTP module will report the exception. If not, handle it explicitly.
                        // http://blogs.msdn.com/b/visualstudioalm/archive/2014/12/12/application-insights-exception-telemetry.aspx
                        if (context.HttpContext.IsCustomErrorEnabled)
                        {
                            var telemetryClient = new TelemetryClient();
                            telemetryClient.TrackException(context.Exception);
                        }
                    }
                }
                catch (Exception exception)
                {
                    context.Exception = new AggregateException("Failed to send exception telemetry.", exception, context.Exception);
                }
            }
        }
    }
}