// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Caching;
using Microsoft.Owin;

namespace NuGet.Services.BasicSearch.Caching
{
    public class MemoryCacheResponseBodyCache
        : IResponseBodyCache
    {
        private static readonly MemoryCache Cache = new MemoryCache("SearchResponse");
        private readonly TimeSpan _defaultExpiration;

        public MemoryCacheResponseBodyCache(TimeSpan defaultExpiration)
        {
            _defaultExpiration = defaultExpiration;
        }

        public long TotalRequests { get; private set; }
        public long Hits { get; private set; }

        public decimal HitRatio
        {
            get
            {
                return Math.Round(Hits / Math.Max(1, (decimal)TotalRequests), 2);
            }
        }

        public void Add(IOwinRequest request, byte[] content)
        {
            if (ShouldCache(request))
            {
                Cache.Add(GetCacheKey(request), content, DateTimeOffset.UtcNow.Add(_defaultExpiration));
            }
        }

        public bool TryGet(IOwinRequest request, out byte[] contentBytes)
        {
            TotalRequests++;

            if (Contains(request))
            {
                try
                {
                    contentBytes = Get(request);
                    if (contentBytes != null)
                    {
                        Hits++;
                        return true;
                    }
                }
                catch
                {
                    // Ignore - we will assume no cache if this happens
                }
            }

            contentBytes = null;
            return false;
        }

        public void Clear()
        {
            Cache.Trim(100);

            TotalRequests = 0;
            Hits = 0;
        }

        private bool ShouldCache(IOwinRequest request)
        {
            if (request.User.Identity.IsAuthenticated)
            {
                return false;
            }

            return true;
        }

        private string GetCacheKey(IOwinRequest request)
        {
            return string.Format("{0}|{1}|{2}", 
                request.Uri, 
                request.User.Identity.IsAuthenticated ? request.User.Identity.Name : "anon", 
                request.Accept);
        }

        private bool Contains(IOwinRequest request)
        {
            return ShouldCache(request)
                   && Cache.Contains(GetCacheKey(request));
        }

        private byte[] Get(IOwinRequest request)
        {
            var value = Cache.Get(GetCacheKey(request));
            if (value != null)
            {
                return (byte[])value;
            }

            return null;
        }
    }
}