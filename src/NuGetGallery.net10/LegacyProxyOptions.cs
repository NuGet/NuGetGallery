// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NuGetGallery;

public sealed class LegacyProxyOptions
{
    public const string SectionName = "LegacyProxy";
    public const long LegacyMaximumRequestBodySize = 262_144_000;
    public const long NativeIisMaximumRequestBodySize = uint.MaxValue;
    public static readonly TimeSpan LegacyExecutionTimeout = TimeSpan.FromSeconds(110);

    public string Origin { get; set; }
    public List<string> PublicOrigins { get; set; } = new();
    public TimeSpan ActivityTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public long MaximumRequestBodySize { get; set; } = LegacyMaximumRequestBodySize;

    internal static bool TryGetOrigin(LegacyProxyOptions options, out Uri origin)
    {
        return TryGetOrigin(options?.Origin, out origin);
    }

    internal static bool TryGetOrigin(string value, out Uri origin)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out origin)
            && (origin.Scheme == Uri.UriSchemeHttp || origin.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(origin.UserInfo)
            && origin.AbsolutePath == "/"
            && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment))
        {
            return true;
        }

        origin = null;
        return false;
    }

    internal static bool IsValidMaximumRequestBodySize(long value)
    {
        return value >= LegacyMaximumRequestBodySize
            && value <= NativeIisMaximumRequestBodySize;
    }
}

internal sealed class LegacyProxyOptionsValidator : IValidateOptions<LegacyProxyOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public LegacyProxyOptionsValidator(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string name, LegacyProxyOptions options)
    {
        if (!LegacyProxyOptions.TryGetOrigin(options, out Uri origin))
        {
            return ValidateOptionsResult.Fail(
                "LegacyProxy:Origin must be an absolute HTTP(S) origin without credentials, a base path, a query, or a fragment.");
        }

        if (options.PublicOrigins == null || options.PublicOrigins.Count == 0)
        {
            return ValidateOptionsResult.Fail(
                "LegacyProxy:PublicOrigins must contain at least one externally visible application origin.");
        }

        var publicOrigins = new List<Uri>();
        foreach (string value in options.PublicOrigins)
        {
            if (!LegacyProxyOptions.TryGetOrigin(value, out Uri publicOrigin))
            {
                return ValidateOptionsResult.Fail(
                    "Every LegacyProxy:PublicOrigins value must be an absolute HTTP(S) origin without credentials, a base path, a query, or a fragment.");
            }

            publicOrigins.Add(publicOrigin);
        }

        if (options.ActivityTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("LegacyProxy:ActivityTimeout must be positive.");
        }

        if (!_environment.IsDevelopment()
            && options.ActivityTimeout < LegacyProxyOptions.LegacyExecutionTimeout)
        {
            return ValidateOptionsResult.Fail(
                $"LegacyProxy:ActivityTimeout must be at least {LegacyProxyOptions.LegacyExecutionTimeout}.");
        }

        if (!LegacyProxyOptions.IsValidMaximumRequestBodySize(options.MaximumRequestBodySize))
        {
            return ValidateOptionsResult.Fail(
                $"LegacyProxy:MaximumRequestBodySize must be between {LegacyProxyOptions.LegacyMaximumRequestBodySize} and {LegacyProxyOptions.NativeIisMaximumRequestBodySize} bytes. Unlimited request bodies are not supported by native IIS Request Filtering.");
        }

        foreach (Uri listener in GetConfiguredListeners())
        {
            if (IsSameInternalListener(origin, listener))
            {
                return ValidateOptionsResult.Fail(
                    $"LegacyProxy:Origin '{origin.GetLeftPart(UriPartial.Authority)}' resolves to this application's configured listener.");
            }
        }

        foreach (Uri publicOrigin in publicOrigins)
        {
            if (IsSamePublicOrigin(origin, publicOrigin))
            {
                return ValidateOptionsResult.Fail(
                    $"LegacyProxy:Origin '{origin.GetLeftPart(UriPartial.Authority)}' matches public application origin '{publicOrigin.GetLeftPart(UriPartial.Authority)}'.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private IEnumerable<Uri> GetConfiguredListeners()
    {
        var values = new List<string>();
        AddDelimited(values, _configuration["urls"]);
        AddDelimited(values, _configuration["ASPNETCORE_URLS"]);

        foreach (IConfigurationSection endpoint in _configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            AddDelimited(values, endpoint["Url"]);
        }

        AddPortListeners(values, Uri.UriSchemeHttp, _configuration["HTTP_PORTS"]);
        AddPortListeners(values, Uri.UriSchemeHttp, _configuration["ASPNETCORE_HTTP_PORTS"]);
        AddPortListeners(values, Uri.UriSchemeHttps, _configuration["HTTPS_PORTS"]);
        AddPortListeners(values, Uri.UriSchemeHttps, _configuration["ASPNETCORE_HTTPS_PORTS"]);

        if (values.Count == 0)
        {
            values.Add("http://localhost:5000");
        }

        return values
            .Select(value => Uri.TryCreate(NormalizeWildcardUrl(value), UriKind.Absolute, out Uri listener)
                ? listener
                : null)
            .Where(listener => listener != null);
    }

    private static void AddDelimited(List<string> values, string configuredValue)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            values.AddRange(configuredValue.Split(
                new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries));
        }
    }

    private static void AddPortListeners(
        List<string> values,
        string scheme,
        string configuredPorts)
    {
        if (string.IsNullOrWhiteSpace(configuredPorts))
        {
            return;
        }

        foreach (string port in configuredPorts.Split(
            new[] { ';' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            values.Add($"{scheme}://0.0.0.0:{port}");
        }
    }

    private static string NormalizeWildcardUrl(string value)
    {
        return value
            .Trim()
            .Replace("://*:", "://0.0.0.0:", StringComparison.Ordinal)
            .Replace("://+:", "://0.0.0.0:", StringComparison.Ordinal);
    }

    private static bool IsSameInternalListener(Uri origin, Uri listener)
    {
        return origin.Port == listener.Port
            && (IsWildcardHost(listener.Host)
                || origin.Host.Equals(listener.Host, StringComparison.OrdinalIgnoreCase)
                || (IsLoopbackHost(origin.Host) && IsLoopbackHost(listener.Host)));
    }

    private static bool IsSamePublicOrigin(Uri origin, Uri publicOrigin)
    {
        bool sameHost = origin.Host.Equals(publicOrigin.Host, StringComparison.OrdinalIgnoreCase)
            || (IsLoopbackHost(origin.Host) && IsLoopbackHost(publicOrigin.Host));
        bool sameEndpoint = origin.Port == publicOrigin.Port
            || (IsDefaultHttpOrigin(origin) && IsDefaultHttpsOrigin(publicOrigin))
            || (IsDefaultHttpsOrigin(origin) && IsDefaultHttpOrigin(publicOrigin));
        return sameHost && sameEndpoint;
    }

    private static bool IsDefaultHttpOrigin(Uri origin)
    {
        return origin.Scheme == Uri.UriSchemeHttp && origin.Port == 80;
    }

    private static bool IsDefaultHttpsOrigin(Uri origin)
    {
        return origin.Scheme == Uri.UriSchemeHttps && origin.Port == 443;
    }

    private static bool IsWildcardHost(string host)
    {
        return host == "0.0.0.0"
            || host == "::";
    }

    private static bool IsLoopbackHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out IPAddress address) && IPAddress.IsLoopback(address));
    }
}
