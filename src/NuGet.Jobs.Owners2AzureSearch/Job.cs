// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.AzureSearch;

namespace NuGet.Jobs
{
    public class Job : JsonConfigurationJob
    {
        private const string ConfigurationSectionName = "Owners2AzureSearch";

        public override async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            await Task.Yield();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder.AddAzureSearch();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddAzureSearch();

            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
