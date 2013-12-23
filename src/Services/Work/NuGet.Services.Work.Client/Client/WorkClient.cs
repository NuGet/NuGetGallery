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
        
        /// <summary>
        /// Create a work service client from the specified base uri and default credentials
        /// </summary>
        /// <param name="baseUri">The base URI of the service</param>
        public WorkClient(Uri baseUri) : this(baseUri, credentials: null) { }

        /// <summary>
        /// Create a work service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The base URI of the service</param>
        /// <param name="credentials">The credentials to connect to the service with</param>
        public WorkClient(Uri baseUri, ICredentials credentials)
        {
            _client = new HttpClient(new HttpClientHandler()
            {
                Credentials = credentials,
                AllowAutoRedirect = true,
                UseDefaultCredentials = credentials == null
            }, disposeHandler: true);
            _client.BaseAddress = baseUri;

            InitializeResources();
        }

        /// <summary>
        /// Create a work service client from the specified HttpClient. This client MUST have a valid
        /// BaseAddress, as the WorkClient will always use relative URLs to request work service APIs
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
        }
    }
}
