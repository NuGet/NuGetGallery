// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;

namespace NuGetGallery;

internal interface ILegacyProxyHttpClient : IDisposable
{
    HttpMessageInvoker Invoker { get; }
}

internal sealed class LegacyProxyHttpClient : ILegacyProxyHttpClient
{
    public LegacyProxyHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            UseCookies = false,
            UseProxy = false,
        };

        Invoker = new HttpMessageInvoker(handler, disposeHandler: true);
    }

    public HttpMessageInvoker Invoker { get; }

    public void Dispose()
    {
        Invoker.Dispose();
    }
}
