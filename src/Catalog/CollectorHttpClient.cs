// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class CollectorHttpClient : HttpClient
    {
        int _requestCount;

        public CollectorHttpClient()
            : base(new WebRequestHandler { AllowPipelining = true })
        {
            _requestCount = 0;
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
            InReqCount();

            Task<string> task = GetStringAsync(address);
            return task.ContinueWith<JObject>((t) =>
            {
                try
                {
                    return JObject.Parse(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetJObjectAsync({0})", address), e);
                }
            });
        }

        public virtual Task<IGraph> GetGraphAsync(Uri address)
        {
            Task<JObject> task = GetJObjectAsync(address);
            return task.ContinueWith<IGraph>((t) =>
            {
                try
                {
                    return NuGet.Services.Metadata.Catalog.Utils.CreateGraph(t.Result);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("GetGraphAsync({0})", address), e);
                }
            });
        }
    }
}
