// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Db2AzureSearch;

namespace NuGet.Jobs
{
    public class Job : AzureSearchJob<Db2AzureSearchCommand>
    {
        private const string ConfigurationSectionName = "Db2AzureSearch";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureJobServices(services, configurationRoot);

            services.Configure<Db2AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AuxiliaryDataStorageConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
