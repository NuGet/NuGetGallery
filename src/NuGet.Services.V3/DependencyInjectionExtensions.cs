// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery.Diagnostics;

namespace NuGet.Services.V3
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddV3(this IServiceCollection services)
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
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<IV3TelemetryService, V3TelemetryService>();

            return services;
        }
    }
}
