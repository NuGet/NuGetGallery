// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.FeatureFlags;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery.Diagnostics;

namespace NuGet.Services.V3
{
    public static class DependencyInjectionExtensions
    {
        public static void AddV3(this ContainerBuilder containerBuilder)
        {
            JsonConfigurationJob.ConfigureFeatureFlagAutofacServices(containerBuilder);
        }

        public static IServiceCollection AddV3(
            this IServiceCollection services,
            IDictionary<string, string> telemetryGlobalDimensions,
            IConfigurationRoot configurationRoot)
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

            JsonConfigurationJob.ConfigureFeatureFlagServices(services, configurationRoot);
            services.AddTransient<IFeatureFlagTelemetryService, V3TelemetryService>();

            return services;
        }
    }
}
