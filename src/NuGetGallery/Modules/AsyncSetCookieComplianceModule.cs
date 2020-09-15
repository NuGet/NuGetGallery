// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using NuGetGallery.Cookies;

namespace NuGetGallery.Modules
{
    public class AsyncSetCookieComplianceModule : IHttpModule
    {
        internal ICookieComplianceService CookieComplianceService;

        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            CookieComplianceService = DependencyResolver.Current.GetService<ICookieComplianceService>();

            var eventHandlerTaskAsyncHelper = new EventHandlerTaskAsyncHelper(SetCookieComplianceAsync);
            context.AddOnBeginRequestAsync(eventHandlerTaskAsyncHelper.BeginEventHandler, eventHandlerTaskAsyncHelper.EndEventHandler);
        }

        internal async Task SetCookieComplianceAsync(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;

            var request = new HttpRequestWrapper(context.Request);
            if (await CookieComplianceService.CanWriteAnalyticsCookies(request))
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