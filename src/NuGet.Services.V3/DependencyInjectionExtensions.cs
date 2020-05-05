// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Jobs.Validation;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.FeatureFlags;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery;
using NuGetGallery.Diagnostics;
using NuGetGallery.Features;

namespace NuGet.Services.V3
{
    public static class DependencyInjectionExtensions
    {
        private const string FeatureFlagBindingKey = nameof(FeatureFlagBindingKey);

        private static readonly TimeSpan FeatureFlagServerTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan FeatureFlagMaxExecutionTime = TimeSpan.FromMinutes(10);

        public static IServiceCollection AddV3(this IServiceCollection services, IDictionary<string, string> telemetryGlobalDimensions)
        {
            services
                .AddTransient(p => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });

            services
                .AddTransient(p => (HttpMessageHandler)new TelemetryHandler(
                    p.GetRequiredService<ITelemetryService>(),
                    p.GetRequiredService<HttpClientHandler>()));

            services.AddSingleton(p => new HttpClient(p.GetRequiredService<HttpMessageHandler>()));

            services
                .AddTransient<ICatalogClient, CatalogClient>(p => new CatalogClient(
                    p.GetRequiredService<ISimpleHttpClient>(),
                    p.GetRequiredService<ILogger<CatalogClient>>()));

            services.AddTransient<CommitCollectorUtility>();

            services.AddTransient<ICollector, CommitCollectorHost>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IRegistrationClient, RegistrationClient>();
            services.AddTransient<ISimpleHttpClient, SimpleHttpClient>();
            services.AddTransient<ITelemetryService, TelemetryService>(p => new TelemetryService(
                p.GetRequiredService<ITelemetryClient>(),
                telemetryGlobalDimensions));
            services.AddTransient<IV3TelemetryService, V3TelemetryService>();

            return services;
        }

        public static void AddFeatureFlags(this ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<FeatureFlagConfiguration>>();
                    var requestOptions = new BlobRequestOptions
                    {
                        ServerTimeout = FeatureFlagServerTimeout,
                        MaximumExecutionTime = FeatureFlagMaxExecutionTime,
                        LocationMode = LocationMode.PrimaryThenSecondary,
                        RetryPolicy = new ExponentialRetry(),
                    };

                    return new CloudBlobClientWrapper(
                        options.Value.ConnectionString,
                        requestOptions);
                })
                .Keyed<ICloudBlobClient>(FeatureFlagBindingKey);

            containerBuilder
                .Register(c => new CloudBlobCoreFileStorageService(
                    c.ResolveKeyed<ICloudBlobClient>(FeatureFlagBindingKey),
                    c.Resolve<IDiagnosticsService>(),
                    c.Resolve<ICloudBlobContainerInformationProvider>()))
                .Keyed<ICoreFileStorageService>(FeatureFlagBindingKey);

            containerBuilder
                .Register(c => new FeatureFlagFileStorageService(
                    c.ResolveKeyed<ICoreFileStorageService>(FeatureFlagBindingKey)))
                .As<IFeatureFlagStorageService>();
        }

        public static IServiceCollection AddFeatureFlags(this IServiceCollection services)
        {
	        services
                .AddTransient(p =>
                {
                    var options = p.GetRequiredService<IOptionsSnapshot<FeatureFlagConfiguration>>();
                    return new FeatureFlagOptions
                    {
                        RefreshInterval = options.Value.RefreshInternal,
                    };
                });

            services.AddTransient<IFeatureFlagClient, FeatureFlagClient>();
            services.AddTransient<IFeatureFlagTelemetryService, V3TelemetryService>();
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();

            services.AddSingleton<IFeatureFlagCacheService, FeatureFlagCacheService>();
            services.AddSingleton<IFeatureFlagRefresher, FeatureFlagRefresher>();

            return services;
        }
    }
}
