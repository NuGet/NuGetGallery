// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using Yarp.ReverseProxy.Forwarder;

namespace NuGetGallery;

public static class Program
{
    private static readonly TimeSpan SecretCacheDuration = TimeSpan.FromHours(6);

    public static async Task Main(string[] args)
    {
        WebApplication application = BuildApplication(args);
        await application.RunAsync();
    }

    public static WebApplication BuildApplication(
        string[] args,
        Action<WebApplicationBuilder> configureBuilder = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);
        ConfigureSecretInjectedConfiguration(builder, args);

        GalleryHostOptions hostOptions = builder.Configuration
            .GetRequiredSection(GalleryHostOptions.SectionName)
            .Get<GalleryHostOptions>()
            ?? throw new InvalidOperationException("The Gallery host configuration is missing.");
        GalleryForwardedHeadersOptions forwardedHeadersOptions = builder.Configuration
            .GetRequiredSection(GalleryForwardedHeadersOptions.SectionName)
            .Get<GalleryForwardedHeadersOptions>()
            ?? throw new InvalidOperationException("The forwarded headers configuration is missing.");

        builder.Services
            .AddOptions<GalleryHostOptions>()
            .Bind(builder.Configuration.GetRequiredSection(GalleryHostOptions.SectionName))
            .Validate(GalleryHostOptions.IsValid, "Gallery host endpoint paths must be absolute and unique.")
            .ValidateOnStart();
        builder.Services
            .AddOptions<GalleryForwardedHeadersOptions>()
            .Bind(builder.Configuration.GetRequiredSection(GalleryForwardedHeadersOptions.SectionName))
            .Validate(GalleryForwardedHeadersOptions.IsValid, "The forwarded headers configuration is invalid.")
            .ValidateOnStart();
        builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions.Apply);

        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.AddServerHeader = false;
            options.ConfigureHttpsDefaults(httpsOptions =>
            {
                httpsOptions.SslProtocols = context.Configuration.GetValue(
                    "Kestrel:SslProtocols",
                    SslProtocols.Tls12 | SslProtocols.Tls13);
            });

            long? maxRequestBodySize = context.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize");
            if (maxRequestBodySize.HasValue)
            {
                options.Limits.MaxRequestBodySize = maxRequestBodySize.Value < 0
                    ? null
                    : maxRequestBodySize.Value;
            }
        });

        builder.Services.AddGallerySharedAuthentication(
            builder.Configuration,
            builder.Environment.IsProduction());
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();
        builder.Services.AddHttpForwarder();

        WebApplication application = builder.Build();

        application.UseForwardedHeaders();
        application.Use(async (context, next) =>
        {
            context.Response.OnStarting(
                static state =>
                {
                    var response = (HttpResponse)state;
                    response.Headers.Remove("Server");
                    response.Headers.Remove("X-Powered-By");
                    response.Headers.Remove("X-AspNet-Version");
                    response.Headers.Remove("X-AspNetMvc-Version");
                    return Task.CompletedTask;
                },
                context.Response);
            await next(context);
        });
        application.UseAuthentication();
        application.UseAuthorization();

        application.MapGet(hostOptions.LocalPath, () => Results.Text("NuGetGallery.net10"));
        application.MapHealthChecks(
            hostOptions.HealthPath,
            new HealthCheckOptions { Predicate = _ => false });
        application.MapHealthChecks(hostOptions.ReadinessPath);

        return application;
    }

    private static void ConfigureSecretInjectedConfiguration(
        WebApplicationBuilder builder,
        string[] args)
    {
        IConfigurationRoot bootstrapConfiguration = builder.Configuration;
        var baseFactory = new ConfigurationRootSecretReaderFactory(bootstrapConfiguration);
        var cachingFactory = new CachingSecretReaderFactory(baseFactory, SecretCacheDuration);
        ISecretReader secretReader = cachingFactory.CreateSecretReader();
        ISecretInjector secretInjector = cachingFactory.CreateSecretInjector(secretReader);

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddInjectedJsonFile("appsettings.json", secretInjector)
            .AddInjectedJsonFile(
                $"appsettings.{builder.Environment.EnvironmentName}.json",
                secretInjector,
                optional: true)
            .AddInjectedEnvironmentVariables(string.Empty, secretInjector)
            .AddCommandLine(args)
            .Build();

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddSingleton<ISecretReader>(secretReader);
        builder.Services.AddSingleton<ISecretInjector>(secretInjector);
    }
}
