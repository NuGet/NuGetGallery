// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;

namespace NuGetGallery.Infrastructure.Search
{
    public interface IResilientSearchClient
    {
        Task<HttpResponseMessage> GetAsync(string path, string queryString);
    }
}