// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace NuGet.Jobs.Validation
{
    public abstract class ValidationJobBase : JsonConfigurationJob
    {
        private const string PackageDownloadTimeoutName = "PackageDownloadTimeout";

        /// <summary>
        /// The maximum number of concurrent connections that can be established to a single server.
        /// </summary>
        private const int MaximumConnectionsPerServer = 64;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            ServicePointManager.DefaultConnectionLimit = MaximumConnectionsPerServer;
        }

        protected override void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureDefaultJobServices(services, configurationRoot);

            ConfigureDatabaseServices(services);

            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IFileDownloader, PackageDownloader>();

            services.AddTransient<ICloudBlobClient>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<ValidationStorageConfiguration>>();
                return new CloudBlobClientWrapper(
                    configurationAccessor.Value.ConnectionString,
                    readAccessGeoRedundant: false);
            });
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();

            services.AddTransient<ISubscriptionClient>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

                return new SubscriptionClientWrapper(
                    config.ConnectionString,
                    config.TopicPath,
                    config.SubscriptionName,
                    p.GetRequiredService<ILogger<SubscriptionClientWrapper>>());
            });

            services.AddSingleton(p =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var assemblyName = assembly.GetName().Name;
                var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                var client = new HttpClient(new WebRequestHandler
                {
                    AllowPipelining = true,
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
                });

                client.Timeout = configurationRoot.GetValue<TimeSpan>(PackageDownloadTimeoutName);
                client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");

                return client;
            });
        }

        private void ConfigureDatabaseServices(IServiceCollection services)
        {
            services.AddScoped<IValidationEntitiesContext>(p =>
            {
                var connectionFactory = p.GetRequiredService<ISqlConnectionFactory<ValidationDbConfiguration>>();
                var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                return new ValidationEntitiesContext(connection);
            });

            services.AddScoped<IEntitiesContext>(p =>
            {
                var connectionFactory = p.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>();
                DbConnection  connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                return new EntitiesContext(connection, readOnly: true);
            });
        }
    }
}
