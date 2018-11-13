// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.Catalog2AzureSearch;

namespace NuGet.Jobs
{
    public class Job : JsonConfigurationJob
    {
        private const string Db2AzureSearchSectionName = "Catalog2AzureSearch";

        public override Task Run()
        {
            return Task.CompletedTask;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<Catalog2AzureSearchConfiguration>(configurationRoot.GetSection(Db2AzureSearchSectionName));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(Db2AzureSearchSectionName));
        }
    }
}
