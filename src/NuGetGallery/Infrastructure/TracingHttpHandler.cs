using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Infrastructure
{
    public class TracingHttpHandler : DelegatingHandler
    {
        public IDiagnosticsSource Trace { get; private set; }

        public TracingHttpHandler(IDiagnosticsSource trace)
        {
            Trace = trace;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp;
            using (Trace.Activity(request.Method + " " + request.RequestUri.AbsoluteUri))
            {
                resp = await base.SendAsync(request, cancellationToken);
            }

            string message = ((int)resp.StatusCode).ToString() + " " + request.RequestUri.AbsoluteUri;
            if (resp.IsSuccessStatusCode)
            {
                Trace.Information(message);
            } else {
                Trace.Error(message);
            }

            return resp;
        }
    }
}
