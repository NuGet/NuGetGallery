// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    public interface IHttpClientWrapper
    {
        Task<string> GetStringAsync(IEnumerable<Uri> endpoints);

        Task<HttpResponseMessage> GetAsync(IEnumerable<Uri> endpoints);

        HttpClient Client { get; }
    }
}
