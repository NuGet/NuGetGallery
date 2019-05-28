// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services.Telemetry;
using System.Web.Http.ExceptionHandling;

namespace NuGetGallery
{
    public class QuietExceptionLogger : ExceptionLogger
    {
        public override void Log(ExceptionLoggerContext context)
        {
            QuietLog.LogHandledException(context.Exception);
        }
    }
}