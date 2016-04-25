// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;
using SerilogWeb.Classic.Enrichers;

namespace NuGet.Services.BasicSearch
{
    public class CorrelationIdMiddleware 
        : OwinMiddleware
    {
        public const string OwinRequestIdKey = "owin.RequestId";
        public const string CorrelationIdHeaderKey = "X-CorrelationId";

        private static readonly string SerilogRequestIdItemName = typeof(HttpRequestIdEnricher).Name + "+RequestId";

        public CorrelationIdMiddleware(OwinMiddleware next) 
            : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            // Determine correlation id.
            var correlationId = context.Get<string>(OwinRequestIdKey);

            // The NuGet Gallery sends us the X-CorrelationId header.
            // If that header is present, override OWIN's owin.RequestId.
            string[] temp = null;
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out temp))
            {
                correlationId = temp[0];

                context.Set(OwinRequestIdKey, correlationId);
            }

            // As a bonus, make Serilog aware of this request ID as well.
            if (HttpContext.Current != null)
            {
                HttpContext.Current.Items[SerilogRequestIdItemName] = Guid.Parse(correlationId);
            }

            // Run all the things
            await Next.Invoke(context);

            // Set response header
            context.Response.Headers.Add("X-CorrelationId", new[] { context.Get<string>(OwinRequestIdKey) });
        }
    }
}