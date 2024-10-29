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
        private const string HttpDependencyType = "HTTP";

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
            if (item is RequestTelemetry requestTelemetryItem)
            {
                ModifyRequestItem(requestTelemetryItem);
            }

            else if (item is DependencyTelemetry dependencyTelemetryItem)
            {
                ModifyDependencyItem(dependencyTelemetryItem);
            }
        }

        private void ModifyRequestItem(RequestTelemetry requestTelemetryItem)
        {
            // In some cases, Application Insights reports an intermediate request as a workaround 
            // when AI lost correlation context and has to restore it.
            // Hence, RequestTelemetry.Url may be null.
            // See https://github.com/microsoft/ApplicationInsights-dotnet-server/pull/898
            // and https://docs.microsoft.com/en-us/dotnet/api/microsoft.applicationinsights.datacontracts.requesttelemetry.url
            if (requestTelemetryItem.Url == null)
            {
                return;
            }

            var route = GetCurrentRoute();
            if (route == null)
            {
                return;
            }

            requestTelemetryItem.Url = RouteExtensions.ObfuscateUrlQuery(requestTelemetryItem.Url, RouteExtensions.ObfuscatedReturnUrlMetadata);
            // Removes the first /
            var requestPath = requestTelemetryItem.Url.AbsolutePath.TrimStart('/');
            var obfuscatedPath = route.ObfuscateUrlPath(requestPath);
            if (obfuscatedPath != null)
            {
                requestTelemetryItem.Url = new Uri(requestTelemetryItem.Url.ToString().Replace(requestPath, obfuscatedPath));
                requestTelemetryItem.Name = requestTelemetryItem.Name.Replace(requestPath, obfuscatedPath);
                if (requestTelemetryItem.Context.Operation?.Name != null)
                {
                    requestTelemetryItem.Context.Operation.Name = requestTelemetryItem.Context.Operation.Name.Replace(requestPath, obfuscatedPath);
                }
            }
        }

        // Protected and virtual for testing purposes.
        protected virtual Route GetCurrentRoute()
        {
            if (HttpContext.Current == null)
            {
                return null;
            }

            return RouteTable.Routes.GetRouteData(new HttpContextWrapper(HttpContext.Current))?.Route as Route;
        }

        private void ModifyDependencyItem(DependencyTelemetry dependencyTelemetryItem)
        {
            // Obfuscate the hashed email address from Gravatar URLs.
            if (!dependencyTelemetryItem.Type.Equals(HttpDependencyType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Uri.TryCreate(dependencyTelemetryItem.Data, UriKind.Absolute, out var uri))
            {
                return;
            }

            if (!uri.Host.EndsWith("gravatar.com") || !uri.AbsolutePath.StartsWith("/avatar/"))
            {
                return;
            }

            var builder = new UriBuilder(uri);

            builder.Path = "/avatar/Obfuscated";
            dependencyTelemetryItem.Name = "GET /avatar/Obfuscated";
            dependencyTelemetryItem.Data = builder.Uri.ToString();
        }
    }
}