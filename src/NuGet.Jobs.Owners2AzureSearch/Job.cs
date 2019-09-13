// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.Owners2AzureSearch;

namespace NuGet.Jobs
{
    public class Job : AzureSearchJob<Owners2AzureSearchCommand>
    {
        private const string ConfigurationSectionName = "Owners2AzureSearch";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureJobServices(services, configurationRoot);

            services.Configure<AzureSearchJobConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
