// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin;

namespace NuGet.Services.BasicSearch.Caching
{
    public class NullResponseBodyCache
        : IResponseBodyCache
    {
        public long TotalRequests
        {
            get { return 0; }
        }

        public long Hits
        {
            get { return 0; }
        }

        public decimal HitRatio
        {
            get { return 0; }
        }

        public void Add(IOwinRequest request, byte[] content)
        {
            // noop
        }
        
        public bool TryGet(IOwinRequest request, out byte[] contentBytes)
        {
            contentBytes = null;
            return false;
        }

        public void Clear()
        {
            // noop
        }
    }
}