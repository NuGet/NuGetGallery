// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Jobs.Validation;
using NuGet.Services.AzureManagement;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class Job : ValidationJobBase
    {
        private const string AzureManagementSectionName = "AzureManagement";
        private const string MonitorConfigurationSectionName = "MonitorConfiguration";

        public override async Task Run()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var rebooter = scope.ServiceProvider.GetRequiredService<ISearchInstanceRebooter>();
                await rebooter.RunAsync(CancellationToken.None);
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<MonitorConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(configurationRoot.GetSection(MonitorConfigurationSectionName));
            services.Configure<AzureManagementAPIWrapperConfiguration>(configurationRoot.GetSection(AzureManagementSectionName));

            services.AddSingleton<IHttpClientWrapper>(p => new HttpClientWrapper(p.GetService<HttpClient>()));
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISearchInstanceRebooter, SearchInstanceRebooter>();
            services.AddTransient<IFeedClient, FeedClient>();
            services.AddTransient<ISearchServiceClient, SearchServiceClient>();
            services.AddSingleton<IAzureManagementAPIWrapper, AzureManagementAPIWrapper>();
            services.AddTransient<IAzureManagementAPIWrapperConfiguration>(p => p.GetService<IOptionsSnapshot<AzureManagementAPIWrapperConfiguration>>().Value);

            services.AddSingleton(p =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var assemblyName = assembly.GetName().Name;
                var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                var client = new HttpClient(new WebRequestHandler
                {
                    AllowPipelining = true,
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
                    ServerCertificateCustomValidationCallback =
                        (httpRequestMessage, cert, cetChain, policyErrors) =>
                        {
                            if (policyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch || policyErrors == System.Net.Security.SslPolicyErrors.None)
                            {
                                return true;
                            }

                            return false;
                        },
                });

                client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");
                client.Timeout = TimeSpan.FromSeconds(10);

                return client;
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}
