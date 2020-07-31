// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using NuGet.Jobs;
using NuGet.Jobs.Validation;

namespace NuGet.Services.AzureSearch
{
    public abstract class AzureSearchJob<T> : JsonConfigurationJob where T : IAzureSearchCommand
    {
        private const string FeatureFlagConfigurationSectionName = "FeatureFlags";

        public override async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            var featureFlagRefresher = _serviceProvider.GetRequiredService<IFeatureFlagRefresher>();
            await featureFlagRefresher.StartIfConfiguredAsync();

            var tracingInterceptor = _serviceProvider.GetRequiredService<IServiceClientTracingInterceptor>();
            try
            {
                ServiceClientTracing.IsEnabled = true;
                ServiceClientTracing.AddTracingInterceptor(tracingInterceptor);

                await _serviceProvider.GetRequiredService<T>().ExecuteAsync();
            }
            finally
            {
                ServiceClientTracing.RemoveTracingInterceptor(tracingInterceptor);
            }

            await featureFlagRefresher.StopAndWaitAsync();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder.AddAzureSearch();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddAzureSearch(GlobalTelemetryDimensions);

            services.Configure<FeatureFlagConfiguration>(configurationRoot.GetSection(FeatureFlagConfigurationSectionName));
        }
    }
}
