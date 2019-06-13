// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Routing;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGetGallery
{
    public class ClientTelemetryPIIProcessor : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; }

        public ClientTelemetryPIIProcessor(ITelemetryProcessor next)
        {
            this.Next = next;
        }

        public void Process(ITelemetry item)
        {
            ModifyItem(item);
            this.Next.Process(item);
        }

        private void ModifyItem(ITelemetry item)
        {
            var requestTelemetryItem = item as RequestTelemetry;
            if(requestTelemetryItem != null && requestTelemetryItem.Url != null)
            {
                var route = GetCurrentRoute();
                if(route == null)
                {
                    return;
                }
                requestTelemetryItem.Url = RouteExtensions.ObfuscateUrlQuery(requestTelemetryItem.Url, RouteExtensions.ObfuscatedReturnUrlMetadata);
                // Removes the first /
                var requestPath = requestTelemetryItem.Url.AbsolutePath.TrimStart('/');
                var obfuscatedPath = route.ObfuscateUrlPath(requestPath);
                if(obfuscatedPath != null)
                {
                    requestTelemetryItem.Url = new Uri(requestTelemetryItem.Url.ToString().Replace(requestPath, obfuscatedPath));
                    requestTelemetryItem.Name = requestTelemetryItem.Name.Replace(requestPath, obfuscatedPath);
                    if(requestTelemetryItem.Context.Operation?.Name != null)
                    {
                        requestTelemetryItem.Context.Operation.Name = requestTelemetryItem.Context.Operation.Name.Replace(requestPath, obfuscatedPath);
                    }
                }
            }
        }

        public virtual Route GetCurrentRoute()
        {
            if (HttpContext.Current == null)
            {
                return null;
            }

            return RouteTable.Routes.GetRouteData(new HttpContextWrapper(HttpContext.Current))?.Route as Route;
        }
    }
}