// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if NETFRAMEWORK
using VDS.RDF;
#endif

namespace NuGet.Services.Metadata.Catalog
{
    public class CollectorHttpClient : HttpClient
    {
        private int _requestCount;
        private readonly IHttpRetryStrategy _retryStrategy;

        public CollectorHttpClient()
            : this(handler: null)
        {
        }

        public CollectorHttpClient(HttpMessageHandler handler, IHttpRetryStrategy retryStrategy = null)
            : base(handler ?? new HttpClientHandler())
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

#if NETFRAMEWORK
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
#endif

        public virtual async Task<string> GetStringAsync(Uri address, CancellationToken token)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using (var httpResponse = await _retryStrategy.SendAsync(this, address, token))
                {
                    Trace.TraceInformation(nameof(GetStringAsync) + "({0}) got " + nameof(HttpResponseMessage) + " after {1}ms", address, sw.ElapsedMilliseconds);
                    sw.Restart();
                    var response = await NuGet.Jobs.TaskExtensions.ExecuteWithTimeoutAsync(
                        _ => httpResponse.Content.ReadAsStringAsync(),
                        timeout: this.Timeout);
                    Trace.TraceInformation(nameof(GetStringAsync) + "({0}) got string ({1} chars) after {2}ms", address, response.Length, sw.ElapsedMilliseconds);
                    return response;
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{nameof(GetStringAsync)}({address})", e);
            }
        }
    }
}