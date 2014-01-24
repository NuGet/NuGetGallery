using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Client
{
    public class WorkClient
    {
        private HttpClient _client;

        public InvocationsClient Invocations { get; private set; }
        public JobsClient Jobs { get; private set; }
        public WorkersClient Workers { get; private set; }
        
        /// <summary>
        /// Create a work service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public WorkClient(Uri baseUri, params DelegatingHandler[] handlers) : this(baseUri, null, handlers)
        {
        }

        /// <summary>
        /// Create a work service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="credentials">The credentials to connect to the service with</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public WorkClient(Uri baseUri, ICredentials credentials, params DelegatingHandler[] handlers)
        {
            // Link the handlers
            HttpMessageHandler handler = new HttpClientHandler()
            {
                Credentials = credentials,
                AllowAutoRedirect = true,
                UseDefaultCredentials = credentials == null
            };

            foreach (var providedHandler in handlers.Reverse())
            {
                providedHandler.InnerHandler = handler;
                handler = providedHandler;
            }

            _client = new HttpClient(handler, disposeHandler: true);
            _client.BaseAddress = baseUri;

            InitializeResources();
        }

        /// <summary>
        /// Create a work service client from the specified HttpClient. This client MUST have a valid
        /// BaseAddress, as the WorkClient will always use relative URLs to request work service APIs.
        /// The BaseAddress should point at the root of the service, NOT at the work service node.
        /// </summary>
        /// <param name="client">The client to use</param>
        public WorkClient(HttpClient client)
        {
            _client = client;

            InitializeResources();
        }

        private void InitializeResources()
        {
            Invocations = new InvocationsClient(_client);
            Jobs = new JobsClient(_client);
            Workers = new WorkersClient(_client);
        }
    }
}
