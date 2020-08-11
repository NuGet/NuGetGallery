// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class CollectorHttpClient : HttpClient
    {
        private int _requestCount;
        private readonly IHttpRetryStrategy _retryStrategy;

        public CollectorHttpClient()
            : this(new WebRequestHandler { AllowPipelining = true })
        {
        }

        public CollectorHttpClient(HttpMessageHandler handler, IHttpRetryStrategy retryStrategy = null)
            : base(handler ?? new WebRequestHandler { AllowPipelining = true })
        {
            _requestCount = 0;
            _retryStrategy = retryStrategy ?? new RetryWithExponentialBackoff();
        }

        public int RequestCount
        {
            get { return _requestCount; }
        }

        protected void InReqCount()
        {
            Interlocked.Increment(ref _requestCount);
        }

        public virtual Task<JObject> GetJObjectAsync(Uri address)
        {
            return GetJObjectAsync(address, CancellationToken.None);
        }

        public virtual async Task<JObject> GetJObjectAsync(Uri address, CancellationToken token)
        {
            InReqCount();

            var json = await GetStringAsync(address, token);

            try
            {
                return ParseJObject(json);
            }
            catch (Exception e)
            {
                throw new Exception($"{nameof(GetJObjectAsync)}({address})", e);
            }
        }

        private static JObject ParseJObject(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.DateTimeOffset; // make sure we always preserve timezone info

                return JObject.Load(reader);
            }
        }

        public virtual Task<IGraph> GetGraphAsync(Uri address)
        {
            return GetGraphAsync(address, readOnly: false, token: CancellationToken.None);
        }

        public virtual Task<IGraph> GetGraphAsync(Uri address, bool readOnly, CancellationToken token)
        {
            var task = GetJObjectAsync(address, token);

            return task.ContinueWith((t) =>
            {
                try
                {
                    return Utils.CreateGraph(t.Result, readOnly);
                }
                catch (Exception e)
                {
                    throw new Exception($"{nameof(GetGraphAsync)}({address})", e);
                }
            }, token);
        }

        public virtual async Task<string> GetStringAsync(Uri address, CancellationToken token)
        {
            try
            {
                using (var httpResponse = await _retryStrategy.SendAsync(this, address, token))
                {
                    return await httpResponse.Content.ReadAsStringAsync();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{nameof(GetStringAsync)}({address})", e);
            }
        }
    }
}