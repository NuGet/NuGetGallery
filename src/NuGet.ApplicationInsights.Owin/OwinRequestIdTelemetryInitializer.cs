// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.ApplicationInsights.Owin
{
    public class OwinRequestIdTelemetryInitializer
        : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            var owinRequestId = OwinRequestIdContext.Get();
            if (owinRequestId != null)
            {
                telemetry.Context.Operation.Id = owinRequestId;
            }
        }
    }
}