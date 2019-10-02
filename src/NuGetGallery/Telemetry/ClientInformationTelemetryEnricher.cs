// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGetGallery
{
    public class ClientInformationTelemetryEnricher : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            var request = telemetry as RequestTelemetry;

            if (request != null)
            {
                var httpContext = GetHttpContext();
                if (httpContext != null && httpContext.Request != null)
                {
                    // Note that telemetry initializers can be called multiple times for the same telemetry item, so
                    // these operations need to not fail if called again. In this particular case, Dictionary.Add
                    // cannot be used since it will fail if the key already exists.
                    // https://github.com/microsoft/ApplicationInsights-dotnet-server/issues/977

                    // ClientVersion is available for NuGet clients starting version 4.1.0-~4.5.0 
                    // Was deprecated and replaced by Protocol version
                    telemetry.Context.Properties[TelemetryService.ClientVersion]
                        = httpContext.Request.Headers[ServicesConstants.ClientVersionHeaderName];

                    telemetry.Context.Properties[TelemetryService.ProtocolVersion]
                        = httpContext.Request.Headers[ServicesConstants.NuGetProtocolHeaderName];

                    telemetry.Context.Properties[TelemetryService.ClientInformation]
                        = httpContext.GetClientInformation();

                    // Is the user authenticated or this is an anonymous request?
                    telemetry.Context.Properties[TelemetryService.IsAuthenticated]
                        = httpContext.Request.IsAuthenticated.ToString();
                }
            }
        }

        protected virtual HttpContextBase GetHttpContext()
        {
            return new HttpContextWrapper(HttpContext.Current);
        }
    }
}