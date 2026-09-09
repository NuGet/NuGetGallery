// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;

namespace NuGetGallery;

internal sealed class LegacyProxyForwarder
{
    private readonly IHttpForwarder _forwarder;
    private readonly ILegacyProxyHttpClient _httpClient;
    private readonly LegacyProxyTransformer _transformer;
    private readonly string _destinationPrefix;
    private readonly ForwarderRequestConfig _requestConfig;

    public LegacyProxyForwarder(
        IHttpForwarder forwarder,
        ILegacyProxyHttpClient httpClient,
        LegacyProxyTransformer transformer,
        IOptions<LegacyProxyOptions> options)
    {
        _forwarder = forwarder;
        _httpClient = httpClient;
        _transformer = transformer;
        _destinationPrefix = options.Value.Origin.TrimEnd('/');
        _requestConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = options.Value.ActivityTimeout,
            AllowResponseBuffering = false,
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
    }

    public async Task ForwardAsync(HttpContext context)
    {
        if (!LegacyProxyTransformer.TryGetSafeRawTarget(context, out _))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await _forwarder.SendAsync(
            context,
            _destinationPrefix,
            _httpClient.Invoker,
            _requestConfig,
            _transformer,
            context.RequestAborted);
    }
}
