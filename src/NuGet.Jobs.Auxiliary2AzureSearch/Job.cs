// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.Auxiliary2AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Jobs
{
    public class Job : AzureSearchJob<Auxiliary2AzureSearchCommand>
    {
        private const string ConfigurationSectionName = "Auxiliary2AzureSearch";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureJobServices(services, configurationRoot);

            services.Configure<Auxiliary2AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AuxiliaryDataStorageConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
