// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;

namespace NuGetGallery
{
    public class ClientTelemetryPIIProcessor : ITelemetryProcessor
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        private const string DefaultTelemetryUserName = "username";

        private ITelemetryProcessor Next { get; set; }

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
            if(item != null && item is RequestTelemetry)
            {
                ((RequestTelemetry)item).Url = GetUri((RequestTelemetry)item);
            }
        }

        private Uri GetUri(RequestTelemetry telemetryItem)
        {
            if(IsPIIOperation(telemetryItem.Context.Operation.Name))
            {
                return new Uri($"{telemetryItem.Url.Scheme}://{telemetryItem.Url.Host}");
            }
            var httpContext = GetHttpContext();
            bool requestIsAuthenticated = httpContext != null ? (httpContext.Request != null ? httpContext.Request.IsAuthenticated : false) : false;
            if(requestIsAuthenticated)
            {
                var currentUserName = GetOwingContext(httpContext).GetCurrentUser().Username;
                var uriString = telemetryItem.Url.ToString().Replace(currentUserName, DefaultTelemetryUserName);
                return new Uri(uriString);
            }
            return telemetryItem.Url;
        }

        protected virtual bool IsPIIOperation(string operationName)
        {
            string[] piiActions = new string[]{"GET Users/Profiles",
                "GET Users/ResetPassword",
                "POST Users/ResetPassword",
                "GET Users/Delete",
                "POST Users/Delete",
                "GET Packages/ConfirmPendingOwnershipRequest",
                "GET Packages/RejectPendingOwnershipRequest",
                "GET Packages/CancelPendingOwnershipRequest"};
            return piiActions.Contains(operationName);
        }

        protected virtual HttpContextBase GetHttpContext()
        {
            return new HttpContextWrapper(HttpContext.Current);
        }

        protected virtual IOwinContext GetOwingContext(HttpContextBase httpContext)
        {
            return httpContext.GetOwinContext();
        }
    }
}