// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    /// <summary>
    /// A wrapper around HttpClient to be used by the Search.
    /// It enables better unit testing and typed dependency injection pattern for the search http clients. 
    /// </summary>
    public interface ISearchHttpClient
    {
        Task<HttpResponseMessage> GetAsync(Uri uri);

        Uri BaseAddress { get; }
    }
}