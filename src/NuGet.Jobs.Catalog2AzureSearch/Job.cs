// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;
using NuGet.Services.V3;

namespace NuGet.Jobs
{
    public class Job : AzureSearchJob<Catalog2AzureSearchCommand>
    {
        private const string ConfigurationSectionName = "Catalog2AzureSearch";
        private const string DevelopmentConfigurationSectionName = "Catalog2AzureSearch:Development";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureJobServices(services, configurationRoot);

            services.Configure<Catalog2AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<CommitCollectorConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchJobDevelopmentConfiguration>(
                configurationRoot.GetSection(DevelopmentConfigurationSectionName));
        }
    }
}
