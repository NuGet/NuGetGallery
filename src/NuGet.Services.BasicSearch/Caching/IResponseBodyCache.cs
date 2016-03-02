// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin;

namespace NuGet.Services.BasicSearch.Caching
{
    public interface IResponseBodyCache
    {
        /// <summary>
        /// Total requests to cache (not thread safe, not locked - to avoid blocking request paths)
        /// </summary>
        long TotalRequests { get; }

        /// <summary>
        /// Total cache hits (not thread safe, not locked - to avoid blocking request paths)
        /// </summary>
        long Hits { get; }

        /// <summary>
        /// Cache hit ratio percentage
        /// </summary>
        decimal HitRatio { get; }
        
        void Add(IOwinRequest request, byte[] content);
        bool TryGet(IOwinRequest request, out byte[] contentBytes);
        void Clear();
    }
}