using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd
{
    public class ConsoleHttpTraceHandler : DelegatingHandler
    {
        public IConsole Console { get; private set; }

        public ConsoleHttpTraceHandler(IConsole console)
            : base()
        {
            Console = console;
        }

        public ConsoleHttpTraceHandler(IConsole console, HttpMessageHandler handler)
            : base(handler)
        {
            Console = console;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            await Console.WriteHttpLine("{0} {1}", request.Method.ToString().ToUpperInvariant(), request.RequestUri.AbsoluteUri);
            var response = await base.SendAsync(request, cancellationToken);
            await Console.WriteHttpLine("{0} {1}", (int)response.StatusCode, request.RequestUri.AbsoluteUri);
            return response;
        }
    }
}
