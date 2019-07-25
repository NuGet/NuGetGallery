// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Db2AzureSearch;

namespace NuGet.Jobs
{
    public class Job : JsonConfigurationJob
    {
        private const string ConfigurationSectionName = "Db2AzureSearch";

        public override async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            await _serviceProvider
                .GetRequiredService<Db2AzureSearchCommand>()
                .ExecuteAsync();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder.AddAzureSearch();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddAzureSearch();

            services.Configure<Db2AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.AddTransient<IOptionsSnapshot<IAuxiliaryDataStorageConfiguration>>(
                p => p.GetRequiredService<IOptionsSnapshot<Db2AzureSearchConfiguration>>());
            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
