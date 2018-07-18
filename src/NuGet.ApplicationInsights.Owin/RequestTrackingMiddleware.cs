// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;

namespace NuGet.ApplicationInsights.Owin
{
    public class RequestTrackingMiddleware
        : OwinMiddleware
    {
        public const string OwinRequestIdKey = "owin.RequestId";

        private readonly TelemetryClient _telemetryClient;

        public RequestTrackingMiddleware(OwinMiddleware next)
            : this(next, null)
        {
        }

        public RequestTrackingMiddleware(OwinMiddleware next, TelemetryConfiguration telemetryConfiguration) 
            : base(next)
        {
            _telemetryClient = telemetryConfiguration == null 
                ? new TelemetryClient()
                : new TelemetryClient(telemetryConfiguration);
        }

        public override async Task Invoke(IOwinContext context)
        {
            var requestId = context.Get<string>(OwinRequestIdKey);

            var requestMethod = context.Request.Method;
            var requestPath = context.Request.Path.ToString();
            var requestUri = context.Request.Uri;

            var requestStartDate = DateTimeOffset.Now;
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var requestFailed = false;

            try
            {
                OwinRequestIdContext.Set(requestId);

                if (Next != null)
                {
                    await Next.Invoke(context);
                }
            }
            catch (Exception ex)
            {
                requestFailed = true;

                _telemetryClient.TrackException(ex);

                throw;
            }
            finally
            {
                stopWatch.Stop();

                TrackRequest(requestId, requestMethod, requestPath, requestUri, 
                    context.Response?.StatusCode ?? 0, requestFailed, requestStartDate, stopWatch.Elapsed);

                OwinRequestIdContext.Clear();
            }
        }

        private void TrackRequest(string requestId, string requestMethod, string path, Uri uri, int responseCode,
            bool requestFailed, DateTimeOffset requestStartDate, TimeSpan duration)
        {
            var name = $"{requestMethod} {path}";

            var telemetry = new RequestTelemetry
            {
                Id = requestId,
                Name = name,
                Timestamp = requestStartDate,
                Duration = duration,
                ResponseCode = responseCode.ToString(),
                Success = (responseCode >= 200 && responseCode <= 299) || !requestFailed,
                Url = uri
            };

            telemetry.Context.Operation.Name = name;

            _telemetryClient.TrackRequest(telemetry);
        }
    }
}