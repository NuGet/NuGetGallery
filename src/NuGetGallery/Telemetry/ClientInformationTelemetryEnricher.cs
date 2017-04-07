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
        public const string ClientVersionPropertyKey = "ClientVersion";
        public const string ClientInfoPropertyKey = "ClientInfo";

        public void Initialize(ITelemetry telemetry)
        {
            var request = telemetry as RequestTelemetry;

            if (request != null)
            {
                var httpContext = GetHttpContext();
                if (httpContext != null && httpContext.Request != null)
                {
                    // ClientVersion will be available for NuGet clients starting version 4.1.0
                    telemetry.Context.Properties.Add(
                        ClientVersionPropertyKey,
                        httpContext.Request.Headers[Constants.ClientVersionHeaderName]);

                    // Best effort attempt to extract client information from the user-agent header.
                    // According to documentation here: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/User-Agent 
                    // the common structure for user-agent header is:
                    // User-Agent: Mozilla/<version> (<system-information>) <platform> (<platform-details>) <extensions>
                    // Thus, extracting the part before the first '(', should give us product and version tokens in MOST cases.
                    string userAgent = httpContext.Request.Headers[Constants.UserAgentHeaderName];
                    telemetry.Context.Properties.Add(ClientInfoPropertyKey, GetProductInformation(userAgent));
                }
            }
            
        }

        protected virtual HttpContextBase GetHttpContext()
        {
            return new HttpContextWrapper(HttpContext.Current);
        }

        private string GetProductInformation(string userAgent)
        {
            string result = string.Empty;

            if (!string.IsNullOrEmpty(userAgent))
            {
                int commentPartStartIndex = userAgent.IndexOf('(');

                if (commentPartStartIndex != -1)
                {
                    result = userAgent.Substring(0, commentPartStartIndex);
                }
                else
                {
                    result = userAgent;
                }

                result = result.Trim();
            }

            return result;
        }
    }
}