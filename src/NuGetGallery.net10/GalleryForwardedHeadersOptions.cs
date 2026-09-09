// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace NuGetGallery;

public sealed class GalleryForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";

    public int? ForwardLimit { get; set; } = 1;
    public List<string> TrustedProxies { get; set; } = new();
    public List<string> TrustedNetworks { get; set; } = new();
    public List<string> AllowedHosts { get; set; } = new();

    public static bool IsValid(GalleryForwardedHeadersOptions options)
    {
        return options != null
            && (options.TrustedProxies ?? new()).All(value => IPAddress.TryParse(value, out _))
        && (options.TrustedNetworks ?? new()).All(value => System.Net.IPNetwork.TryParse(value, out _))
            && (options.AllowedHosts ?? new()).All(value =>
                !string.IsNullOrWhiteSpace(value)
                && value != "*");
    }

    public void Apply(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedHost
            | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ForwardLimit < 0 ? null : ForwardLimit;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.AllowedHosts.Clear();

        foreach (string proxy in TrustedProxies ?? new())
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (string network in TrustedNetworks ?? new())
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }

        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.1/32"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("::1/128"));
        }

        foreach (string host in AllowedHosts ?? new())
        {
            options.AllowedHosts.Add(host);
        }

        if (options.AllowedHosts.Count == 0)
        {
            options.ForwardedHeaders &= ~ForwardedHeaders.XForwardedHost;
        }
    }
}
