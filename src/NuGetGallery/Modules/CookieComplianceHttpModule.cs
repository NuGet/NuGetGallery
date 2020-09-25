// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Threading.Tasks;
using NuGetGallery.Cookies;

namespace NuGetGallery.Modules
{
    public class CookieComplianceHttpModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            var eventHandlerTaskAsyncHelper = new EventHandlerTaskAsyncHelper(SetCookieComplianceAsync);
            context.AddOnBeginRequestAsync(eventHandlerTaskAsyncHelper.BeginEventHandler, eventHandlerTaskAsyncHelper.EndEventHandler);
        }

        private async Task SetCookieComplianceAsync(object sender, EventArgs e)
        {
            var httpApplication = sender as HttpApplication;
            if (httpApplication == null)
            {
                return;
            }

            var context = httpApplication.Context;
            if (context == null)
            {
                return;
            }

            var request = context.Request;
            if (request == null)
            {
                return;
            }

            var requestWrapper = new HttpRequestWrapper(request);
            if (await CookieComplianceService.Instance.CanWriteAnalyticsCookiesAsync(requestWrapper))
            {
                context.Items.Add(ServicesConstants.CookieComplianceCanWriteAnalyticsCookies, true);
            }
            else
            {
                context.Items.Add(ServicesConstants.CookieComplianceCanWriteAnalyticsCookies, false);
            }
        }
    }
}