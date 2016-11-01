// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class CollectorHttpClient : HttpClient
    {
        private int _requestCount;

        public CollectorHttpClient()
            : this(new WebRequestHandler { AllowPipelining = true })
        {
        }

        public CollectorHttpClient(HttpMessageHandler handler)
            : base(handler ?? new WebRequestHandler { AllowPipelining = true })
        {
            _requestCount = 0;
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

        public virtual Task<JObject> GetJObjectAsync(Uri address, CancellationToken token)
        {
            InReqCount();

            var task = GetStringAsync(address, token);
            return task.ContinueWith((t) =>
            {
                try
                {
                    return ParseJObject(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetJObjectAsync({0})", address), e);
                }
            }, token);
        }

        private JObject ParseJObject(string json)
        {
            using (JsonReader reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.DateTimeOffset; // make sure we always preserve timezone info

                return JObject.Load(reader);
            }
        }

        public virtual Task<IGraph> GetGraphAsync(Uri address)
        {
            return GetGraphAsync(address, CancellationToken.None);
        }

        public virtual Task<IGraph> GetGraphAsync(Uri address, CancellationToken token)
        {
            var task = GetJObjectAsync(address, token);
            return task.ContinueWith((t) =>
            {
                try
                {
                    return Utils.CreateGraph(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetGraphAsync({0})", address), e);
                }
            }, token);
        }

        public virtual Task<string> GetStringAsync(Uri address, CancellationToken token)
        {
            var task = GetAsync(address, token);
            return task.ContinueWith((t) =>
            {
                try
                {
                    return task.Result.Content.ReadAsStringAsync().Result;
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetStringAsync({0})", address), e);
                }
            }, token);
        }
    }
}
