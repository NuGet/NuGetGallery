// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;

namespace NuGetGallery;

internal sealed class LegacyProxyTransformer : HttpTransformer
{
    private readonly Uri _legacyOrigin;

    public LegacyProxyTransformer(IOptions<LegacyProxyOptions> options)
    {
        if (!LegacyProxyOptions.TryGetOrigin(options.Value, out _legacyOrigin))
        {
            throw new InvalidOperationException("The legacy proxy origin is invalid.");
        }
    }

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(
            httpContext,
            proxyRequest,
            destinationPrefix,
            cancellationToken);

        if (!TryGetSafeRawTarget(httpContext, out string rawTarget))
        {
            throw new InvalidOperationException(
                "The legacy proxy forwarder received an unvalidated raw request target.");
        }

        proxyRequest.RequestUri = new Uri(
            destinationPrefix.TrimEnd('/') + rawTarget,
            new UriCreationOptions
            {
                DangerousDisablePathAndQueryCanonicalization = true,
            });
        proxyRequest.Headers.Host = httpContext.Request.Host.Value;

        RemoveForwardingHeaders(proxyRequest);

        string clientAddress = FormatForwardedAddress(httpContext.Connection.RemoteIpAddress);
        string publicHost = httpContext.Request.Host.Value;
        string publicScheme = httpContext.Request.Scheme;

        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", clientAddress);
        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", publicHost);
        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", publicScheme);
        proxyRequest.Headers.TryAddWithoutValidation(
            "Forwarded",
            $"for={Quote(clientAddress)};host={Quote(publicHost)};proto={publicScheme}");
    }

    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage proxyResponse,
        CancellationToken cancellationToken)
    {
        bool copyResponse = await base.TransformResponseAsync(
            httpContext,
            proxyResponse,
            cancellationToken);

        if (copyResponse
            && httpContext.Response.Headers.TryGetValue(HeaderNames.Location, out var values)
            && Uri.TryCreate(values.ToString(), UriKind.Absolute, out Uri location)
            && IsLegacyOrigin(location))
        {
            var publicLocation = new UriBuilder(location)
            {
                Scheme = httpContext.Request.Scheme,
                Host = httpContext.Request.Host.Host,
                Port = httpContext.Request.Host.Port ?? -1,
            };
            httpContext.Response.Headers.Location = publicLocation.Uri.AbsoluteUri;
        }

        return copyResponse;
    }

    private static void RemoveForwardingHeaders(HttpRequestMessage proxyRequest)
    {
        proxyRequest.Headers.Remove("Forwarded");
        proxyRequest.Headers.Remove("X-Forwarded-For");
        proxyRequest.Headers.Remove("X-Forwarded-Host");
        proxyRequest.Headers.Remove("X-Forwarded-Proto");
        proxyRequest.Headers.Remove("X-Forwarded-Prefix");
    }

    internal static bool TryGetSafeRawTarget(
        HttpContext context,
        out string rawTarget)
    {
        rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (string.IsNullOrEmpty(rawTarget) || rawTarget[0] != '/')
        {
            rawTarget = context.Request.PathBase
                + context.Request.Path
                + context.Request.QueryString;
        }

        return IsSafeRawTarget(rawTarget);
    }

    private static bool IsSafeRawTarget(string rawTarget)
    {
        if (string.IsNullOrEmpty(rawTarget) || rawTarget[0] != '/')
        {
            return false;
        }

        for (int index = 0; index < rawTarget.Length; index++)
        {
            char character = rawTarget[index];
            if (character <= ' '
                || character >= '\u007f'
                || character == '#'
                || character == '\\')
            {
                return false;
            }

            if (character == '%'
                && (index + 2 >= rawTarget.Length
                    || !IsHexadecimal(rawTarget[index + 1])
                    || !IsHexadecimal(rawTarget[index + 2])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHexadecimal(char character)
    {
        return character >= '0' && character <= '9'
            || character >= 'A' && character <= 'F'
            || character >= 'a' && character <= 'f';
    }

    private static string FormatForwardedAddress(IPAddress address)
    {
        if (address == null)
        {
            return "unknown";
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();
    }

    private static string Quote(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private bool IsLegacyOrigin(Uri location)
    {
        return location.Scheme.Equals(_legacyOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
            && location.Host.Equals(_legacyOrigin.Host, StringComparison.OrdinalIgnoreCase)
            && location.Port == _legacyOrigin.Port;
    }
}
